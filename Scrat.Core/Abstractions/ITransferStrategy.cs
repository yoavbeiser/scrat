using Scrat.Core.Models;

namespace Scrat.Core.Abstractions;

/// <summary>Moves the data of one key from an S3 endpoint to an exporter.</summary>
public interface ITransferStrategy
{
    SizeCategory Handles { get; }

    Task ExecuteAsync(S3EndpointMatch match, string key, IExporter exporter, CancellationToken cancellationToken = default);
}
