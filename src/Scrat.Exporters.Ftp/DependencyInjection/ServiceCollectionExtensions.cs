using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scrat.Core.DependencyInjection;
using Scrat.Exporters.Ftp.Abstractions;
using Scrat.Exporters.Ftp.FluentFtp;

namespace Scrat.Exporters.Ftp.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the FTP exporter (wrapped in per-action resilience) and its options.</summary>
    public static IServiceCollection AddFtpExporter(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<FtpOptions>(configuration.GetSection(FtpOptions.SectionName));
        services.TryAddSingleton<IFtpConnectionFactory, FluentFtpConnectionFactory>();
        services.AddResilientExporter<FtpExporter>();
        return services;
    }
}
