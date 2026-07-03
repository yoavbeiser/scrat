using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Scrat.Core.Abstractions;
using Scrat.Core.Models;
using Scrat.Core.S3;

namespace Scrat.Core.Tests.S3;

public class S3EndpointCompositeTests
{
    private static IS3Endpoint CreateEndpoint(SizeCategory category, string? bucket, bool bucketExists)
    {
        var reader = Substitute.For<IS3Reader>();
        reader.BucketExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(bucketExists);

        var endpoint = Substitute.For<IS3Endpoint>();
        endpoint.HandledSizeCategory.Returns(category);
        endpoint.Reader.Returns(reader);
        endpoint.ResolveBucketName(Arg.Any<string>()).Returns(bucket);
        return endpoint;
    }

    private static S3EndpointComposite CreateComposite(params IS3Endpoint[] endpoints) =>
        new(endpoints, NullLogger<S3EndpointComposite>.Instance);

    [Fact]
    public async Task Returns_first_cluster_whose_bucket_exists_in_ascending_size_order()
    {
        var small = CreateEndpoint(SizeCategory.Small, "small-bucket", bucketExists: true);
        var medium = CreateEndpoint(SizeCategory.Medium, "medium-bucket", bucketExists: true);

        // Registration order must not matter.
        var composite = CreateComposite(medium, small);

        var match = await composite.FindEndpointAsync("key");

        Assert.NotNull(match);
        Assert.Equal(SizeCategory.Small, match.Endpoint.HandledSizeCategory);
        Assert.Equal("small-bucket", match.Bucket);
        await medium.Reader.DidNotReceive().BucketExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Falls_through_to_larger_cluster_when_smaller_bucket_is_missing()
    {
        var small = CreateEndpoint(SizeCategory.Small, "small-bucket", bucketExists: false);
        var large = CreateEndpoint(SizeCategory.Large, "large-bucket", bucketExists: true);

        var match = await CreateComposite(small, large).FindEndpointAsync("key");

        Assert.NotNull(match);
        Assert.Equal(SizeCategory.Large, match.Endpoint.HandledSizeCategory);
    }

    [Fact]
    public async Task Skips_endpoints_whose_naming_convention_rejects_the_key()
    {
        var medium = CreateEndpoint(SizeCategory.Medium, bucket: null, bucketExists: true);
        var large = CreateEndpoint(SizeCategory.Large, "large-bucket", bucketExists: true);

        var match = await CreateComposite(medium, large).FindEndpointAsync("key");

        Assert.NotNull(match);
        Assert.Equal(SizeCategory.Large, match.Endpoint.HandledSizeCategory);
        await medium.Reader.DidNotReceive().BucketExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_null_when_no_cluster_holds_the_key()
    {
        var small = CreateEndpoint(SizeCategory.Small, "small-bucket", bucketExists: false);

        Assert.Null(await CreateComposite(small).FindEndpointAsync("key"));
    }
}
