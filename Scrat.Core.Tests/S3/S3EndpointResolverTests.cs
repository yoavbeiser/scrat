using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Scrat.Core.Deserialization.Abstractions;
using Scrat.Core.Models;
using Scrat.Core.S3;
using Scrat.Core.S3.Abstractions;
using Scrat.Core.Tests.TestDoubles;

namespace Scrat.Core.Tests.S3;

public class S3EndpointResolverTests
{
    private static IS3Endpoint CreateEndpoint(SizeCategory category, string? bucket, bool objectExists)
    {
        var reader = Substitute.For<IS3Reader>();
        reader.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(objectExists);

        var endpoint = Substitute.For<IS3Endpoint>();
        endpoint.HandledSizeCategory.Returns(category);
        endpoint.Reader.Returns(reader);
        endpoint.BucketInfo.Returns(new BucketInfo(_ => bucket));
        return endpoint;
    }

    private static S3EndpointResolver CreateResolver(params IS3Endpoint[] endpoints) =>
        new(endpoints, NullLogger<S3EndpointResolver>.Instance);

    [Fact]
    public async Task Returns_first_cluster_that_holds_the_key_in_ascending_size_order()
    {
        var small = CreateEndpoint(SizeCategory.Small, "small-bucket", objectExists: true);
        var medium = CreateEndpoint(SizeCategory.Medium, "medium-bucket", objectExists: true);

        // Registration order must not matter.
        var resolver = CreateResolver(medium, small);

        var match = await resolver.FindEndpointAsync("key");

        Assert.NotNull(match);
        Assert.Equal(SizeCategory.Small, match.Endpoint.HandledSizeCategory);
        Assert.Equal("small-bucket", match.Bucket);
        await medium.Reader.DidNotReceive().ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Falls_through_to_larger_cluster_when_smaller_cluster_lacks_the_key()
    {
        var small = CreateEndpoint(SizeCategory.Small, "small-bucket", objectExists: false);
        var large = CreateEndpoint(SizeCategory.Large, "large-bucket", objectExists: true);

        var match = await CreateResolver(small, large).FindEndpointAsync("key");

        Assert.NotNull(match);
        Assert.Equal(SizeCategory.Large, match.Endpoint.HandledSizeCategory);
    }

    [Fact]
    public async Task Skips_endpoints_whose_naming_convention_rejects_the_key()
    {
        var medium = CreateEndpoint(SizeCategory.Medium, bucket: null, objectExists: true);
        var large = CreateEndpoint(SizeCategory.Large, "large-bucket", objectExists: true);

        var match = await CreateResolver(medium, large).FindEndpointAsync("key");

        Assert.NotNull(match);
        Assert.Equal(SizeCategory.Large, match.Endpoint.HandledSizeCategory);
        await medium.Reader.DidNotReceive().ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_null_when_no_cluster_holds_the_key()
    {
        var small = CreateEndpoint(SizeCategory.Small, "small-bucket", objectExists: false);

        Assert.Null(await CreateResolver(small).FindEndpointAsync("key"));
    }
}

/// <summary>Routing exercised through the concrete endpoints + a real (in-memory) reader.</summary>
public class S3EndpointResolverIntegrationTests
{
    private static S3EndpointResolver CreateResolver(FakeS3Reader reader)
    {
        var deserializer = Substitute.For<IDataDeserializer>();
        IS3Endpoint[] endpoints =
        [
            new SmallS3Endpoint(reader, deserializer),
            new MediumS3Endpoint(reader, deserializer),
            new LargeS3Endpoint(reader),
        ];
        return new S3EndpointResolver(endpoints, NullLogger<S3EndpointResolver>.Instance);
    }

    [Fact]
    public async Task A_large_shaped_key_present_only_on_large_is_not_misrouted_to_small()
    {
        // "video-1" fits both Small (small-data-vi) and Large (large-data-video). The object only
        // lives on the Large cluster, so bucket-only probing would misroute it — object probing must not.
        var reader = new FakeS3Reader().Put("large-data-video", "video-1", [1, 2, 3]);

        var match = await CreateResolver(reader).FindEndpointAsync("video-1");

        Assert.NotNull(match);
        Assert.Equal(SizeCategory.Large, match.Endpoint.HandledSizeCategory);
        Assert.Equal("large-data-video", match.Bucket);
    }

    [Fact]
    public async Task Small_wins_when_it_actually_holds_the_key()
    {
        var reader = new FakeS3Reader().Put("small-data-vi", "video-1", [1, 2, 3]);

        var match = await CreateResolver(reader).FindEndpointAsync("video-1");

        Assert.NotNull(match);
        Assert.Equal(SizeCategory.Small, match.Endpoint.HandledSizeCategory);
    }

    [Fact]
    public async Task Key_present_on_no_cluster_resolves_to_null()
    {
        var reader = new FakeS3Reader(); // nothing stored

        Assert.Null(await CreateResolver(reader).FindEndpointAsync("video-1"));
    }
}
