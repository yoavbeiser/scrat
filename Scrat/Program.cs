using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrat.Core.DependencyInjection;
using Scrat.Core.Models;
using Scrat.Core.Services;
using Scrat.Core.Services.Abstractions;
using Scrat.Exporters.Ftp.DependencyInjection;
using Scrat.Exporters.Smb.DependencyInjection;

if (args.Length < 2 || !Enum.TryParse<ExporterType>(args[0], ignoreCase: true, out var exporterType))
{
    Console.Error.WriteLine("Usage: scrat <smb|ftp> <key1> [key2 key3 ...]");
    return 1;
}

var keys = args[1..];

// Anchor configuration next to the binary so the CLI behaves the same from any working directory.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Logging.ClearProviders();
builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddScratCore(builder.Configuration);
builder.Services.AddSmbExporter(builder.Configuration);
builder.Services.AddFtpExporter(builder.Configuration);

using var host = builder.Build();

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

TransferResult result;
try
{
    var service = host.Services.GetRequiredService<IScratService>();
    result = await service.ExecuteAsync(new TransferRequest(exporterType, keys), cancellation.Token);
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.Error.WriteLine("Cancelled.");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

foreach (var key in result.Keys)
{
    Console.WriteLine(key.Status switch
    {
        KeyStatus.Ok => $"[OK       ] {key.Key}",
        KeyStatus.NotFound => $"[NOT_FOUND] {key.Key}",
        _ => $"[FAILED   ] {key.Key}  ({key.Error})",
    });
}

Console.WriteLine();
Console.WriteLine($"{result.OkCount} ok  |  {result.NotFoundCount} not found  |  {result.FailedCount} failed");

// Exit codes let callers tell a real error from missing input:
//   0 = every key transferred, 1 = at least one key failed, 2 = some keys not found (none failed).
return result.FailedCount > 0 ? 1
    : result.NotFoundCount > 0 ? 2
    : 0;
