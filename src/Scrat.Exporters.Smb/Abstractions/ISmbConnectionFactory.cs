namespace Scrat.Exporters.Smb.Abstractions;

/// <summary>Opens authenticated connections to the configured share.</summary>
public interface ISmbConnectionFactory
{
    Task<ISmbShareConnection> ConnectAsync(CancellationToken cancellationToken = default);
}
