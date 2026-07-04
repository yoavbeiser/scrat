using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Polly.Registry;
using Scrat.Core.Configuration;
using Scrat.Core.DependencyInjection;
using Scrat.Core.Exceptions;
using Scrat.Core.Exporting.Abstractions;
using Scrat.Core.Models;
using Scrat.Core.Resilience;
using Scrat.Core.S3.Abstractions;

namespace Scrat.Core.Tests.Resilience;

public class ResilienceTests
{
    private static ResiliencePipelineProvider<string> CreateProvider(int maxRetryAttempts = 3)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(Options.Create(new ResilienceOptions
        {
            MaxRetryAttempts = maxRetryAttempts,
            BaseDelayMs = 1,
            MaxDelayMs = 2,
            AttemptTimeoutSeconds = 30,
            UseJitter = false,
        }));
        services.AddScratResiliencePipelines();
        return services.BuildServiceProvider().GetRequiredService<ResiliencePipelineProvider<string>>();
    }

    [Fact]
    public void Every_action_has_its_own_registered_pipeline()
    {
        var provider = CreateProvider();

        foreach (var name in ResiliencePipelineNames.All)
        {
            Assert.NotNull(provider.GetPipeline(name));
        }
    }

    [Fact]
    public async Task Reader_retries_transient_failures_and_recovers()
    {
        var attempts = 0;
        var inner = Substitute.For<IS3Reader>();
        inner.ReadAllAsync("bucket", "key", Arg.Any<CancellationToken>())
            .Returns(_ => ++attempts < 3 ? throw new IOException("flaky") : Task.FromResult(new byte[] { 42 }));

        var reader = new ResilientS3Reader(inner, CreateProvider());

        var result = await reader.ReadAllAsync("bucket", "key");

        Assert.Equal([42], result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task Reader_gives_up_after_max_attempts()
    {
        var inner = Substitute.For<IS3Reader>();
        inner.GetObjectSizeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<long>>(_ => throw new IOException("always down"));

        var reader = new ResilientS3Reader(inner, CreateProvider(maxRetryAttempts: 2));

        await Assert.ThrowsAsync<IOException>(() => reader.GetObjectSizeAsync("bucket", "key"));
        await inner.Received(3).GetObjectSizeAsync("bucket", "key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Chunked_read_retries_each_range_fetch_independently()
    {
        var failedOnce = false;
        var inner = Substitute.For<IS3Reader>();
        inner.GetObjectSizeAsync("bucket", "key", Arg.Any<CancellationToken>()).Returns(4L);
        inner.ReadRangeAsync("bucket", "key", Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (call.ArgAt<long>(2) == 2 && !failedOnce)
                {
                    failedOnce = true;
                    throw new IOException("flaky second range");
                }

                return Task.FromResult(new byte[call.ArgAt<int>(3)]);
            });

        var reader = new ResilientS3Reader(inner, CreateProvider());

        var chunks = new List<int>();
        await foreach (var chunk in ((IS3Reader)reader).ReadChunksAsync("bucket", "key", 2))
        {
            chunks.Add(chunk.Length);
        }

        Assert.Equal([2, 2], chunks);
        Assert.True(failedOnce);
        await inner.Received(3).ReadRangeAsync("bucket", "key", Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exporter_retries_stream_chunk_writes()
    {
        var attempts = 0;
        var inner = Substitute.For<IExporter>();
        inner.WriteChunkAsync(Arg.Any<string>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++attempts < 2 ? throw new ExportException("broken pipe") : Task.CompletedTask);

        var exporter = new ResilientExporter(inner, CreateProvider());

        await exporter.WriteChunkAsync("key", new byte[3]);

        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task Non_retryable_failures_are_not_retried()
    {
        var inner = Substitute.For<IExporter>();
        inner.WriteAsync(Arg.Any<ExportData>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new NonRetryableExportException("cannot resume"));

        var exporter = new ResilientExporter(inner, CreateProvider());

        await Assert.ThrowsAsync<NonRetryableExportException>(() =>
            exporter.WriteAsync(new ExportData(new byte[1]), "key"));
        await inner.Received(1).WriteAsync(Arg.Any<ExportData>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

public class TransientErrorDetectorTests
{
    [Fact]
    public void Io_and_unknown_failures_are_transient()
    {
        Assert.True(TransientErrorDetector.IsTransient(new IOException()));
        Assert.True(TransientErrorDetector.IsTransient(new HttpRequestException()));
        Assert.True(TransientErrorDetector.IsTransient(new ExportException("broken")));
    }

    [Fact]
    public void Cancellation_programming_errors_and_non_retryable_failures_are_not()
    {
        Assert.False(TransientErrorDetector.IsTransient(null));
        Assert.False(TransientErrorDetector.IsTransient(new OperationCanceledException()));
        Assert.False(TransientErrorDetector.IsTransient(new NonRetryableExportException("stop")));
        Assert.False(TransientErrorDetector.IsTransient(new ArgumentException("bug")));
        Assert.False(TransientErrorDetector.IsTransient(new InvalidDataException("bad payload")));
    }
}
