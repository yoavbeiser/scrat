using System.Text.Json;
using Scrat.Core.Abstractions;
using Scrat.Core.Models;

namespace Scrat.Core.Deserialization;

/// <summary>
/// Small-tier wire format: a JSON document with a base64 <c>payload</c> and an optional
/// <c>metadata</c> object of string values.
/// </summary>
public sealed class JsonPayloadDeserializer : IDataDeserializer
{
    public ExportData Deserialize(ReadOnlyMemory<byte> raw)
    {
        using var document = ParseDocument(raw);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("payload", out var payloadElement)
            || payloadElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("Small-tier object is missing the base64 'payload' string property.");
        }

        if (!payloadElement.TryGetBytesFromBase64(out var content))
        {
            throw new InvalidDataException("Small-tier 'payload' property is not valid base64.");
        }

        return new ExportData(content, ReadMetadata(root));
    }

    private static JsonDocument ParseDocument(ReadOnlyMemory<byte> raw)
    {
        try
        {
            return JsonDocument.Parse(raw);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Small-tier object is not valid JSON.", ex);
        }
    }

    private static IReadOnlyDictionary<string, string>? ReadMetadata(JsonElement root)
    {
        if (!root.TryGetProperty("metadata", out var metadataElement) || metadataElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var metadata = new Dictionary<string, string>();
        foreach (var property in metadataElement.EnumerateObject())
        {
            metadata[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()!
                : property.Value.GetRawText();
        }

        return metadata;
    }
}
