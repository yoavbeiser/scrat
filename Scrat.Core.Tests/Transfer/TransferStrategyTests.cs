using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Scrat.Core.Configuration;
using Scrat.Core.Deserialization;
using Scrat.Core.Deserialization.Abstractions;
using Scrat.Core.Models;
using Scrat.Core.S3.Abstractions;
using Scrat.Core.Tests.TestDoubles;
using Scrat.Core.Transfer;

namespace Scrat.Core.Tests.Transfer;

public class SmallTransferStrategyTests
{
    [Fact]
    public async Task Reads_all_deserializes_and_writes_atomically()
    {
        var reader = new FakeS3Reader().Put("bucket", "key", """{"payload":"AQID"}"""u8.ToArray());
        var endpoint = Substitute.For<IS3Endpoint>();
        endpoint.Reader.Returns(reader);
        endpoint.Deserializer.Returns(new JsonPayloadDeserializer());

        var exporter = new RecordingExporter();
        var strategy = new SmallTransferStrategy(NullLogger<SmallTransferStrategy>.Instance);

        await strategy.ExecuteAsync(new S3EndpointMatch(endpoint, "bucket"), "key", exporter);

        var (data, key) = Assert.Single(exporter.Writes);
        Assert.Equal("key", key);
        Assert.Equal(new byte[] { 1, 2, 3 }, data.Content.ToArray());
    }

    [Fact]
    public async Task Fails_loudly_when_endpoint_has_no_deserializer()
    {
        var reader = new FakeS3Reader().Put("bucket", "key", [1, 2, 3, 4, 5]);
        var endpoint = Substitute.For<IS3Endpoint>();
        endpoint.Reader.Returns(reader);
        endpoint.Deserializer.Returns((IDataDeserializer?)null);

        var strategy = new SmallTransferStrategy(NullLogger<SmallTransferStrategy>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.ExecuteAsync(new S3EndpointMatch(endpoint, "bucket"), "key", new RecordingExporter()));
    }
}

public class MediumTransferStrategyTests
{
    [Fact]
    public async Task Buffers_chunked_reads_deserializes_and_writes_the_payload()
    {
        // 4-byte header + 6 content bytes, read in 4-byte chunks (3 ranged reads).
        var raw = new byte[] { 0, 0, 0, 0, 1, 2, 3, 4, 5, 6 };
        var reader = new FakeS3Reader().Put("bucket", "key", raw);
        var endpoint = Substitute.For<IS3Endpoint>();
        endpoint.Reader.Returns(reader);
        endpoint.Deserializer.Returns(new BinaryHeaderDeserializer());

        var options = Options.Create(new TransferOptions { MediumReadChunkSizeBytes = 4 });
        var exporter = new RecordingExporter();
        var strategy = new MediumTransferStrategy(options, NullLogger<MediumTransferStrategy>.Instance);

        await strategy.ExecuteAsync(new S3EndpointMatch(endpoint, "bucket"), "key", exporter);

        Assert.Equal(3, reader.ReadRangeCalls);
        // The exporter decides how to slice the write, so the strategy just hands it the full payload.
        var (data, key) = Assert.Single(exporter.Writes);
        Assert.Equal("key", key);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, data.Content.ToArray());
    }
}

public class LargeTransferStrategyTests
{
    private static LargeTransferStrategy CreateStrategy(int chunkSize) => new(
        Options.Create(new TransferOptions { LargeChunkSizeBytes = chunkSize }),
        NullLogger<LargeTransferStrategy>.Instance);

    private static S3EndpointMatch Match(FakeS3Reader reader)
    {
        var endpoint = Substitute.For<IS3Endpoint>();
        endpoint.Reader.Returns(reader);
        return new S3EndpointMatch(endpoint, "bucket");
    }

    [Fact]
    public async Task Opens_streams_each_chunk_then_closes()
    {
        var reader = new FakeS3Reader().Put("bucket", "key", Enumerable.Range(0, 20).Select(i => (byte)i).ToArray());
        var exporter = new RecordingExporter();

        await CreateStrategy(chunkSize: 8).ExecuteAsync(Match(reader), "key", exporter);

        Assert.Equal("key", Assert.Single(exporter.Opened));
        Assert.Equal([8, 8, 4], exporter.StreamChunks.Select(c => c.Length));
        Assert.Equal("key", Assert.Single(exporter.Closed));
        Assert.Empty(exporter.AbortedKeys);
    }

    [Fact]
    public async Task Single_chunk_object_opens_writes_once_and_closes()
    {
        var reader = new FakeS3Reader().Put("bucket", "key", [1, 2, 3]);
        var exporter = new RecordingExporter();

        await CreateStrategy(chunkSize: 8).ExecuteAsync(Match(reader), "key", exporter);

        Assert.Single(exporter.Opened);
        Assert.Equal([3], exporter.StreamChunks.Select(c => c.Length));
        Assert.Single(exporter.Closed);
    }

    [Fact]
    public async Task Empty_object_still_opens_and_closes_the_destination_file()
    {
        var reader = new FakeS3Reader().Put("bucket", "key", []);
        var exporter = new RecordingExporter();

        await CreateStrategy(chunkSize: 8).ExecuteAsync(Match(reader), "key", exporter);

        Assert.Single(exporter.Opened);
        Assert.Empty(exporter.StreamChunks);
        Assert.Single(exporter.Closed);
    }

    [Fact]
    public async Task Aborts_the_exporter_stream_when_a_chunk_write_fails()
    {
        var reader = new FakeS3Reader().Put("bucket", "key", new byte[20]);
        var exporter = new RecordingExporter { FailOnStreamChunkIndex = 1 };

        await Assert.ThrowsAsync<IOException>(() =>
            CreateStrategy(chunkSize: 8).ExecuteAsync(Match(reader), "key", exporter));

        Assert.Equal("key", Assert.Single(exporter.AbortedKeys));
    }
}
