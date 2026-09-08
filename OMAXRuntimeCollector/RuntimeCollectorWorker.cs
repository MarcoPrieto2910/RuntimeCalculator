using Microsoft.Extensions.Hosting;

namespace OMAXRuntimeCollector;

public class RuntimeCollectorWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Our existing collector logic will move here.
    }
}