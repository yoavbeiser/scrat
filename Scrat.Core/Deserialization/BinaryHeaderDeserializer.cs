using Scrat.Core.Deserialization.Abstractions;
using Scrat.Core.Models;

namespace Scrat.Core.Deserialization;

/// <summary>Medium-tier wire format: a 4-byte header prefix followed by the content bytes.</summary>
public sealed class BinaryHeaderDeserializer : IDataDeserializer
{
    public const int HeaderLengthBytes = 4;

    public ExportData Deserialize(ReadOnlyMemory<byte> raw)
    {
        if (raw.Length < HeaderLengthBytes)
        {
            throw new InvalidDataException(
                $"Medium-tier object is {raw.Length} bytes; expected at least the {HeaderLengthBytes}-byte header.");
        }

        return new ExportData(raw[HeaderLengthBytes..]);
    }
}
