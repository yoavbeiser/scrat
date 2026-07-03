using System.Collections.Frozen;
using Scrat.Core.Abstractions;
using Scrat.Core.Models;

namespace Scrat.Core.Exporting;

/// <summary>Resolves an exporter type to the matching registered <see cref="IExporter"/> singleton.</summary>
public sealed class ExporterResolver : IExporterResolver
{
    private readonly FrozenDictionary<ExporterType, IExporter> _exporters;

    public ExporterResolver(IEnumerable<IExporter> exporters)
    {
        ArgumentNullException.ThrowIfNull(exporters);
        _exporters = exporters.ToFrozenDictionary(e => e.Type);
    }

    public IExporter Resolve(ExporterType type) =>
        _exporters.TryGetValue(type, out var exporter)
            ? exporter
            : throw new NotSupportedException($"No exporter registered for '{type}'.");
}
