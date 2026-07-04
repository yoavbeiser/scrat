using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrat.Core.Abstractions;
using Scrat.Core.DependencyInjection;
using Scrat.Core.Models;
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
    // CR: status labels are not column-aligned ([OK      ] vs [NOT_FOUND] vs [FAILED  ]) — output looks ragged.
    Console.WriteLine(key.Status switch
    {
        KeyStatus.Ok => $"[OK      ] {key.Key}",
        KeyStatus.NotFound => $"[NOT_FOUND] {key.Key}",
        _ => $"[FAILED  ] {key.Key}  ({key.Error})",
    });
}

Console.WriteLine();
Console.WriteLine($"{result.OkCount} ok  |  {result.NotFoundCount} not found  |  {result.FailedCount} failed");

// CR: AllSucceeded is true only when every key is Ok, so a NotFound key yields exit code 1 — i.e.
//     "key doesn't exist" is reported the same as a real transfer failure. Confirm that's intended;
//     callers often want to distinguish missing input from an actual error.
return result.AllSucceeded ? 0 : 1;
