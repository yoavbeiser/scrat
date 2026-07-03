using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scrat.Core.DependencyInjection;
using Scrat.Exporters.Smb.Abstractions;
using Scrat.Exporters.Smb.SmbLibrary;

namespace Scrat.Exporters.Smb.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the SMB exporter (wrapped in per-action resilience) and its options.</summary>
    public static IServiceCollection AddSmbExporter(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<SmbOptions>(configuration.GetSection(SmbOptions.SectionName));
        services.TryAddSingleton<ISmbConnectionFactory, SmbLibraryConnectionFactory>();
        services.AddResilientExporter<SmbExporter>();
        return services;
    }
}
