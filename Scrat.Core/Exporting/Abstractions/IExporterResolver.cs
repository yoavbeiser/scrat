using Scrat.Core.Models;

namespace Scrat.Core.Exporting.Abstractions;

/// <summary>Resolves an <see cref="ExporterType"/> to the matching <see cref="IExporter"/> singleton.</summary>
public interface IExporterResolver
{
    IExporter Resolve(ExporterType type);
}
