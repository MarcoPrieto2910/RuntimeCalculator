using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OMAXRuntimeCollector;

// Configure the generic host used to run the collector as a Windows Service.
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "OMAX Runtime Collector";
});

builder.Services.AddHostedService<RuntimeCollectorWorker>();

IHost host = builder.Build();
host.Run();