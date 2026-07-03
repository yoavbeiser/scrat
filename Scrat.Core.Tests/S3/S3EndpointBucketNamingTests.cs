using NSubstitute;
using Scrat.Core.Abstractions;
using Scrat.Core.Deserialization;
using Scrat.Core.S3;

namespace Scrat.Core.Tests.S3;

public class S3EndpointBucketNamingTests
{
    private static readonly IS3Reader Reader = Substitute.For<IS3Reader>();

    [Theory]
    [InlineData("ABcdef", "small-data-ab")]
    [InlineData("my/object/key", "small-data-my")]
    [InlineData("a", "small-data-a")]
    public void Small_derives_bucket_from_first_two_chars(string key, string expected)
    {
        var endpoint = new SmallS3Endpoint(Reader, new JsonPayloadDeserializer());
        Assert.Equal(expected, endpoint.ResolveBucketName(key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Small_rejects_blank_keys(string key)
    {
        var endpoint = new SmallS3Endpoint(Reader, new JsonPayloadDeserializer());
        Assert.Null(endpoint.ResolveBucketName(key));
    }

    [Theory]
    [InlineData("2024-06-01/report", "medium-data-2024-06-01")]
    [InlineData("1999-12-31/a/b", "medium-data-1999-12-31")]
    public void Medium_derives_bucket_from_date_prefix(string key, string expected)
    {
        var endpoint = new MediumS3Endpoint(Reader, new BinaryHeaderDeserializer());
        Assert.Equal(expected, endpoint.ResolveBucketName(key));
    }

    [Theory]
    [InlineData("no-slash-here")]
    [InlineData("2024-13-01/bad-month")]
    [InlineData("notadate/name")]
    [InlineData("2024-06-01/")]
    [InlineData("/name")]
    public void Medium_rejects_keys_without_valid_date_prefix(string key)
    {
        var endpoint = new MediumS3Endpoint(Reader, new BinaryHeaderDeserializer());
        Assert.Null(endpoint.ResolveBucketName(key));
    }

    [Theory]
    [InlineData("video-12345", "large-data-video")]
    [InlineData("a-b-c", "large-data-a")]
    public void Large_derives_bucket_from_type_prefix(string key, string expected)
    {
        var endpoint = new LargeS3Endpoint(Reader);
        Assert.Equal(expected, endpoint.ResolveBucketName(key));
    }

    [Theory]
    [InlineData("nodash")]
    [InlineData("-id")]
    [InlineData("type-")]
    public void Large_rejects_keys_without_type_id_shape(string key)
    {
        var endpoint = new LargeS3Endpoint(Reader);
        Assert.Null(endpoint.ResolveBucketName(key));
    }

    [Fact]
    public void Large_has_no_deserializer()
    {
        Assert.Null(new LargeS3Endpoint(Reader).Deserializer);
    }
}
