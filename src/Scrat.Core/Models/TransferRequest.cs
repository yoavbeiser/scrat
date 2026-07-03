namespace Scrat.Core.Models;

/// <summary>A request to transfer a set of S3 keys to a destination.</summary>
public sealed record TransferRequest(ExporterType Exporter, IReadOnlyList<string> Keys);
