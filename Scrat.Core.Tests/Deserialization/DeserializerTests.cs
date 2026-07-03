using System.Text;
using System.Text.Json;
using Scrat.Core.Deserialization;

namespace Scrat.Core.Tests.Deserialization;

public class JsonPayloadDeserializerTests
{
    private readonly JsonPayloadDeserializer _sut = new();

    private static byte[] Envelope(object envelope) => JsonSerializer.SerializeToUtf8Bytes(envelope);

    [Fact]
    public void Decodes_base64_payload_and_string_metadata()
    {
        var raw = Envelope(new
        {
            payload = Convert.ToBase64String("hello"u8.ToArray()),
            metadata = new { source = "unit-test", version = 2 },
        });

        var data = _sut.Deserialize(raw);

        Assert.Equal("hello", Encoding.UTF8.GetString(data.Content.Span));
        Assert.NotNull(data.Metadata);
        Assert.Equal("unit-test", data.Metadata["source"]);
        Assert.Equal("2", data.Metadata["version"]);
    }

    [Fact]
    public void Metadata_is_null_when_absent()
    {
        var data = _sut.Deserialize(Envelope(new { payload = Convert.ToBase64String([1, 2, 3]) }));

        Assert.Null(data.Metadata);
        Assert.Equal(new byte[] { 1, 2, 3 }, data.Content.ToArray());
    }

    [Fact]
    public void Missing_payload_throws()
    {
        Assert.Throws<InvalidDataException>(() => _sut.Deserialize(Envelope(new { metadata = new { } })));
    }

    [Fact]
    public void Invalid_base64_throws()
    {
        Assert.Throws<InvalidDataException>(() => _sut.Deserialize(Envelope(new { payload = "not base64 !!!" })));
    }

    [Fact]
    public void Invalid_json_throws()
    {
        Assert.Throws<InvalidDataException>(() => _sut.Deserialize("not json"u8.ToArray()));
    }
}

public class BinaryHeaderDeserializerTests
{
    private readonly BinaryHeaderDeserializer _sut = new();

    [Fact]
    public void Skips_the_four_byte_header()
    {
        var data = _sut.Deserialize(new byte[] { 0, 1, 2, 3, 10, 20, 30 });

        Assert.Equal(new byte[] { 10, 20, 30 }, data.Content.ToArray());
    }

    [Fact]
    public void Header_only_object_yields_empty_content()
    {
        Assert.Equal(0, _sut.Deserialize(new byte[4]).Content.Length);
    }

    [Fact]
    public void Object_shorter_than_header_throws()
    {
        Assert.Throws<InvalidDataException>(() => _sut.Deserialize(new byte[] { 1, 2, 3 }));
    }
}
