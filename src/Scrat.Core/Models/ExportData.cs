namespace Scrat.Core.Models;

/// <summary>Deserialized payload ready to be written to a destination.</summary>
public sealed record ExportData(ReadOnlyMemory<byte> Content, IReadOnlyDictionary<string, string>? Metadata = null);
