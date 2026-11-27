using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var b = Host.CreateApplicationBuilder(args);
b.Services.AddSingleton(Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(32)
    { SingleReader = true, FullMode = BoundedChannelFullMode.Wait }));
b.Services.AddHostedService<Consumer>();
using var host = b.Build();
await host.StartAsync();
var queue = host.Services.GetRequiredService<Channel<WorkItem>>();
for (int i = 1; i <= 4; i++) await queue.Writer.WriteAsync(new(i));
queue.Writer.Complete();
await host.WaitForShutdownAsync();

record WorkItem(int Id);
sealed class Consumer(Channel<WorkItem> queue, ILogger<Consumer> log, IHostApplicationLifetime lifetime)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(token))
            log.LogInformation("Processed job {Id}", job.Id);
        lifetime.StopApplication();
    }
}
