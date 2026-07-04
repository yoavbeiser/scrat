using NSubstitute;
using Scrat.Core.Deserialization.Abstractions;
using Scrat.Core.S3;
using Scrat.Core.S3.Abstractions;

namespace Scrat.Core.Tests.S3;

public class BucketInfoTests
{
    [Theory]
    [InlineData("ABcdef", "small-data-ab")]
    [InlineData("my/object/key", "small-data-my")]
    [InlineData("a", "small-data-a")]
    public void Small_derives_bucket_from_first_two_chars(string key, string expected)
    {
        Assert.Equal(expected, BucketInfo.Small.Resolve(key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Small_rejects_blank_keys(string key)
    {
        Assert.Null(BucketInfo.Small.Resolve(key));
    }

    [Theory]
    [InlineData("2024-06-01/report", "medium-data-2024-06-01")]
    [InlineData("1999-12-31/a/b", "medium-data-1999-12-31")]
    public void Medium_derives_bucket_from_date_prefix(string key, string expected)
    {
        Assert.Equal(expected, BucketInfo.Medium.Resolve(key));
    }

    [Theory]
    [InlineData("no-slash-here")]
    [InlineData("2024-13-01/bad-month")]
    [InlineData("notadate/name")]
    [InlineData("2024-06-01/")]
    [InlineData("/name")]
    public void Medium_rejects_keys_without_valid_date_prefix(string key)
    {
        Assert.Null(BucketInfo.Medium.Resolve(key));
    }

    [Theory]
    [InlineData("video-12345", "large-data-video")]
    [InlineData("a-b-c", "large-data-a")]
    public void Large_derives_bucket_from_type_prefix(string key, string expected)
    {
        Assert.Equal(expected, BucketInfo.Large.Resolve(key));
    }

    [Theory]
    [InlineData("nodash")]
    [InlineData("-id")]
    [InlineData("type-")]
    public void Large_rejects_keys_without_type_id_shape(string key)
    {
        Assert.Null(BucketInfo.Large.Resolve(key));
    }
}

public class S3EndpointTests
{
    private static readonly IS3Reader Reader = Substitute.For<IS3Reader>();

    [Fact]
    public void Large_has_no_deserializer()
    {
        Assert.Null(new LargeS3Endpoint(Reader).Deserializer);
    }

    [Fact]
    public void Endpoints_expose_their_tier_bucket_rule()
    {
        Assert.Same(BucketInfo.Small, new SmallS3Endpoint(Reader, Substitute.For<IDataDeserializer>()).BucketInfo);
        Assert.Same(BucketInfo.Medium, new MediumS3Endpoint(Reader, Substitute.For<IDataDeserializer>()).BucketInfo);
        Assert.Same(BucketInfo.Large, new LargeS3Endpoint(Reader).BucketInfo);
    }
}
