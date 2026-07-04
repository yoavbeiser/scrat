using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Scrat.Core.Models;
using Scrat.Core.S3;
using Scrat.Core.S3.Abstractions;

namespace Scrat.Core.Tests.S3;

public class S3EndpointResolverTests
{
    private static IS3Endpoint CreateEndpoint(SizeCategory category, string? bucket, bool bucketExists)
    {
        var reader = Substitute.For<IS3Reader>();
        reader.BucketExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(bucketExists);

        var endpoint = Substitute.For<IS3Endpoint>();
        endpoint.HandledSizeCategory.Returns(category);
        endpoint.Reader.Returns(reader);
        endpoint.BucketInfo.Returns(new BucketInfo(_ => bucket));
        return endpoint;
    }

    private static S3EndpointResolver CreateResolver(params IS3Endpoint[] endpoints) =>
        new(endpoints, NullLogger<S3EndpointResolver>.Instance);

    [Fact]
    public async Task Returns_first_cluster_whose_bucket_exists_in_ascending_size_order()
    {
        var small = CreateEndpoint(SizeCategory.Small, "small-bucket", bucketExists: true);
        var medium = CreateEndpoint(SizeCategory.Medium, "medium-bucket", bucketExists: true);

        // Registration order must not matter.
        var resolver = CreateResolver(medium, small);

        var match = await resolver.FindEndpointAsync("key");

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

        var match = await CreateResolver(small, large).FindEndpointAsync("key");

        Assert.NotNull(match);
        Assert.Equal(SizeCategory.Large, match.Endpoint.HandledSizeCategory);
    }

    [Fact]
    public async Task Skips_endpoints_whose_naming_convention_rejects_the_key()
    {
        var medium = CreateEndpoint(SizeCategory.Medium, bucket: null, bucketExists: true);
        var large = CreateEndpoint(SizeCategory.Large, "large-bucket", bucketExists: true);

        var match = await CreateResolver(medium, large).FindEndpointAsync("key");

        Assert.NotNull(match);
        Assert.Equal(SizeCategory.Large, match.Endpoint.HandledSizeCategory);
        await medium.Reader.DidNotReceive().BucketExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // CR: coverage gap — every endpoint here is a mock with an explicit BucketInfo return, so the real
    //   routing behaviours are never exercised: (1) that BucketInfo.Small matches almost any key and
    //   therefore always wins ordering (the misrouting flagged in S3EndpointResolver), and (2) the
    //   "bucket exists but the key is absent from it" case. Add tests with the concrete Small/Medium/Large
    //   endpoints + a FakeS3Reader to pin down the actual selection semantics.
    [Fact]
    public async Task Returns_null_when_no_cluster_holds_the_key()
    {
        var small = CreateEndpoint(SizeCategory.Small, "small-bucket", bucketExists: false);

        Assert.Null(await CreateResolver(small).FindEndpointAsync("key"));
    }
}
