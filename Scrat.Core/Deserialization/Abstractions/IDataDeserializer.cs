using Scrat.Core.Models;

namespace Scrat.Core.Deserialization.Abstractions;

/// <summary>Converts raw S3 bytes into <see cref="ExportData"/>. One implementation per endpoint wire format.</summary>
public interface IDataDeserializer
{
    ExportData Deserialize(ReadOnlyMemory<byte> raw);
}
