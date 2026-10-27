using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OMAXRuntimeCollector;

// Configure the generic host used to run the collector as a Windows Service.
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Select the configuration file based on the current environment.
// Production remains the default unless another environment is explicitly set.
var configFileName = builder.Environment.IsDevelopment() ? "appsettings.Development.json" : "appsettings.json";
var configFilePath = Path.Combine(AppContext.BaseDirectory, configFileName);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "OMAX Runtime Collector";
});

builder.Services.AddHostedService(_ => new RuntimeCollectorWorker(configFilePath));
IHost host = builder.Build();
host.Run();