using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OMAXRuntimeCollector;


HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "OMAX Runtime Collector";
});

builder.Services.AddHostedService<RuntimeCollectorWorker>();

IHost host = builder.Build();
host.Run();