using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using Polly.Retry;
using Scrat.Core.Configuration;
using Scrat.Core.Deserialization;
using Scrat.Core.Exporting;
using Scrat.Core.Exporting.Abstractions;
using Scrat.Core.Resilience;
using Scrat.Core.S3;
using Scrat.Core.S3.Abstractions;
using Scrat.Core.Services;
using Scrat.Core.Services.Abstractions;
using Scrat.Core.Transfer;
using Scrat.Core.Transfer.Abstractions;

namespace Scrat.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the full transfer pipeline: endpoints, strategies, service and resilience.</summary>
    public static IServiceCollection AddScratCore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<TransferOptions>(configuration.GetSection(TransferOptions.SectionName));
        services.Configure<S3EndpointsOptions>(configuration.GetSection(S3EndpointsOptions.SectionName));
        services.Configure<ResilienceOptions>(configuration.GetSection(ResilienceOptions.SectionName));

        services.TryAddSingleton<AwsS3ReaderFactory>();
        services.TryAddSingleton<IS3ReaderFactory>(sp => new ResilientS3ReaderFactory(
            sp.GetRequiredService<AwsS3ReaderFactory>(),
            sp.GetRequiredService<ResiliencePipelineProvider<string>>()));

        services.AddSingleton<IS3Endpoint>(sp => new SmallS3Endpoint(
            CreateReader(sp, o => o.Small),
            new JsonPayloadDeserializer()));
        services.AddSingleton<IS3Endpoint>(sp => new MediumS3Endpoint(
            CreateReader(sp, o => o.Medium),
            new BinaryHeaderDeserializer()));
        services.AddSingleton<IS3Endpoint>(sp => new LargeS3Endpoint(
            CreateReader(sp, o => o.Large)));

        services.AddSingleton<IS3EndpointResolver, S3EndpointResolver>();

        services.AddSingleton<ITransferStrategy, SmallTransferStrategy>();
        services.AddSingleton<ITransferStrategy, MediumTransferStrategy>();
        services.AddSingleton<ITransferStrategy, LargeTransferStrategy>();
        services.AddSingleton<ITransferStrategySelector, TransferStrategySelector>();

        services.AddSingleton<IExporterResolver, ExporterResolver>();
        services.AddSingleton<IScratService, ScratService>();

        services.AddScratResiliencePipelines();
        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TExporter"/> wrapped in a <see cref="ResilientExporter"/>,
    /// so each of its write actions is retried by its own pipeline.
    /// </summary>
    public static IServiceCollection AddResilientExporter<TExporter>(this IServiceCollection services)
        where TExporter : class, IExporter
    {
        services.TryAddSingleton<TExporter>();
        services.AddSingleton<IExporter>(sp => new ResilientExporter(
            sp.GetRequiredService<TExporter>(),
            sp.GetRequiredService<ResiliencePipelineProvider<string>>()));
        return services;
    }

    /// <summary>One retry+timeout pipeline per atomic action, all tuned via <see cref="ResilienceOptions"/>.</summary>
    public static IServiceCollection AddScratResiliencePipelines(this IServiceCollection services)
    {
        foreach (var pipelineName in ResiliencePipelineNames.All)
        {
            services.AddResiliencePipeline(pipelineName, (builder, context) =>
            {
                var options = context.ServiceProvider.GetRequiredService<IOptions<ResilienceOptions>>().Value;
                var logger = context.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Scrat.Resilience");

                builder
                    .AddRetry(new RetryStrategyOptions
                    {
                        MaxRetryAttempts = options.MaxRetryAttempts,
                        Delay = TimeSpan.FromMilliseconds(options.BaseDelayMs),
                        MaxDelay = TimeSpan.FromMilliseconds(options.MaxDelayMs),
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = options.UseJitter,
                        ShouldHandle = args => ValueTask.FromResult(TransientErrorDetector.IsTransient(args.Outcome.Exception)),
                        OnRetry = args =>
                        {
                            logger.LogWarning(
                                args.Outcome.Exception,
                                "Retry {Attempt}/{Max} for action {Action} after {Delay}",
                                args.AttemptNumber + 1,
                                options.MaxRetryAttempts,
                                pipelineName,
                                args.RetryDelay);
                            return default;
                        },
                    })
                    .AddTimeout(TimeSpan.FromSeconds(options.AttemptTimeoutSeconds));
            });
        }

        return services;
    }

    private static IS3Reader CreateReader(IServiceProvider provider, Func<S3EndpointsOptions, S3EndpointConfig> pick)
    {
        var factory = provider.GetRequiredService<IS3ReaderFactory>();
        var endpoints = provider.GetRequiredService<IOptions<S3EndpointsOptions>>().Value;
        return factory.Create(pick(endpoints));
    }
}
