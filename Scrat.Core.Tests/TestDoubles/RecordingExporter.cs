using Scrat.Core.Exporting;
using Scrat.Core.Exporting.Abstractions;
using Scrat.Core.Models;

namespace Scrat.Core.Tests.TestDoubles;

/// <summary>Records every exporter call; can be told to fail on a given stream chunk index.</summary>
internal sealed class RecordingExporter : IExporter
{
    public ExporterType Type => ExporterType.Smb;

    public List<(ExportData Data, string Key)> Writes { get; } = [];

    public List<string> Opened { get; } = [];

    public List<byte[]> StreamChunks { get; } = [];

    public List<string> Closed { get; } = [];

    public List<string> AbortedKeys { get; } = [];

    public int? FailOnStreamChunkIndex { get; init; }

    public Task WriteAsync(ExportData data, string key, CancellationToken cancellationToken = default)
    {
        Writes.Add((data, key));
        return Task.CompletedTask;
    }

    public Task OpenAsync(string key, CancellationToken cancellationToken = default)
    {
        Opened.Add(key);
        return Task.CompletedTask;
    }

    public Task WriteChunkAsync(string key, ReadOnlyMemory<byte> chunk, CancellationToken cancellationToken = default)
    {
        if (StreamChunks.Count == FailOnStreamChunkIndex)
        {
            throw new IOException("Injected stream failure.");
        }

        StreamChunks.Add(chunk.ToArray());
        return Task.CompletedTask;
    }

    public Task CloseAsync(string key, CancellationToken cancellationToken = default)
    {
        Closed.Add(key);
        return Task.CompletedTask;
    }

    public Task AbortStreamAsync(string key, CancellationToken cancellationToken = default)
    {
        AbortedKeys.Add(key);
        return Task.CompletedTask;
    }
}
