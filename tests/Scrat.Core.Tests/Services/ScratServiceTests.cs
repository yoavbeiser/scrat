using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Scrat.Core.Abstractions;
using Scrat.Core.Configuration;
using Scrat.Core.Models;
using Scrat.Core.Services;

namespace Scrat.Core.Tests.Services;

public class ScratServiceTests
{
    private readonly IS3EndpointComposite _composite = Substitute.For<IS3EndpointComposite>();
    private readonly ITransferStrategySelector _selector = Substitute.For<ITransferStrategySelector>();
    private readonly ITransferStrategy _strategy = Substitute.For<ITransferStrategy>();
    private readonly IExporterResolver _exporterResolver = Substitute.For<IExporterResolver>();
    private readonly IExporter _exporter = Substitute.For<IExporter>();

    private ScratService CreateService()
    {
        _exporterResolver.Resolve(Arg.Any<ExporterType>()).Returns(_exporter);
        _selector.Select(Arg.Any<SizeCategory>()).Returns(_strategy);
        return new ScratService(
            _composite,
            _selector,
            _exporterResolver,
            Options.Create(new TransferOptions { MaxConcurrency = 2 }),
            NullLogger<ScratService>.Instance);
    }

    private S3EndpointMatch MatchFor(SizeCategory category)
    {
        var endpoint = Substitute.For<IS3Endpoint>();
        endpoint.HandledSizeCategory.Returns(category);
        return new S3EndpointMatch(endpoint, "bucket");
    }

    [Fact]
    public async Task Aggregates_ok_not_found_and_failed_outcomes_in_input_order()
    {
        var goodMatch = MatchFor(SizeCategory.Small);
        var badMatch = MatchFor(SizeCategory.Large);
        _composite.FindEndpointAsync("good", Arg.Any<CancellationToken>()).Returns(goodMatch);
        _composite.FindEndpointAsync("missing", Arg.Any<CancellationToken>()).Returns((S3EndpointMatch?)null);
        _composite.FindEndpointAsync("bad", Arg.Any<CancellationToken>()).Returns(badMatch);
        _strategy.ExecuteAsync(Arg.Any<S3EndpointMatch>(), "bad", Arg.Any<IExporter>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new IOException("connection refused"));

        var service = CreateService();

        var result = await service.ExecuteAsync(new TransferRequest(ExporterType.Ftp, ["good", "missing", "bad"]));

        Assert.Equal(["good", "missing", "bad"], result.Keys.Select(k => k.Key));
        Assert.Equal(KeyStatus.Ok, result.Keys[0].Status);
        Assert.Equal(KeyStatus.NotFound, result.Keys[1].Status);
        Assert.Equal(KeyStatus.Failed, result.Keys[2].Status);
        Assert.Equal("connection refused", result.Keys[2].Error);

        Assert.Equal(1, result.OkCount);
        Assert.Equal(1, result.NotFoundCount);
        Assert.Equal(1, result.FailedCount);
        Assert.False(result.AllSucceeded);
    }

    [Fact]
    public async Task Succeeds_when_every_key_transfers()
    {
        var match = MatchFor(SizeCategory.Medium);
        _composite.FindEndpointAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(match);

        var result = await CreateService().ExecuteAsync(new TransferRequest(ExporterType.Smb, ["a", "b"]));

        Assert.True(result.AllSucceeded);
        _exporterResolver.Received(1).Resolve(ExporterType.Smb);
        await _strategy.Received(2).ExecuteAsync(Arg.Any<S3EndpointMatch>(), Arg.Any<string>(), _exporter, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Selects_strategy_matching_the_winning_endpoint_category()
    {
        var match = MatchFor(SizeCategory.Large);
        _composite.FindEndpointAsync("key", Arg.Any<CancellationToken>()).Returns(match);

        await CreateService().ExecuteAsync(new TransferRequest(ExporterType.Smb, ["key"]));

        _selector.Received(1).Select(SizeCategory.Large);
    }

    [Fact]
    public async Task Empty_key_list_is_rejected()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateService().ExecuteAsync(new TransferRequest(ExporterType.Smb, [])));
    }
}
