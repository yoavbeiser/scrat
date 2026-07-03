using Scrat.Core.Abstractions;
using Scrat.Core.Models;

namespace Scrat.Core.Tests.TestDoubles;

/// <summary>Records every exporter call; can be told to fail on a given stream chunk index.</summary>
internal sealed class RecordingExporter : IExporter
{
    public ExporterType Type => ExporterType.Smb;

    public List<(ExportData Data, string Key)> Writes { get; } = [];

    public List<(ExportData Data, string Key, int ChunkSize)> ChunkedWrites { get; } = [];

    public List<(byte[] Chunk, bool IsFirst, bool IsLast)> StreamChunks { get; } = [];

    public List<string> AbortedKeys { get; } = [];

    public int? FailOnStreamChunkIndex { get; init; }

    public Task WriteAsync(ExportData data, string key, CancellationToken cancellationToken = default)
    {
        Writes.Add((data, key));
        return Task.CompletedTask;
    }

    public Task WriteChunkedAsync(ExportData data, string key, int chunkSizeBytes, CancellationToken cancellationToken = default)
    {
        ChunkedWrites.Add((data, key, chunkSizeBytes));
        return Task.CompletedTask;
    }

    public Task WriteStreamChunkAsync(string key, ReadOnlyMemory<byte> chunk, bool isFirst, bool isLast, CancellationToken cancellationToken = default)
    {
        if (StreamChunks.Count == FailOnStreamChunkIndex)
        {
            throw new IOException("Injected stream failure.");
        }

        StreamChunks.Add((chunk.ToArray(), isFirst, isLast));
        return Task.CompletedTask;
    }

    public Task AbortStreamAsync(string key, CancellationToken cancellationToken = default)
    {
        AbortedKeys.Add(key);
        return Task.CompletedTask;
    }
}
