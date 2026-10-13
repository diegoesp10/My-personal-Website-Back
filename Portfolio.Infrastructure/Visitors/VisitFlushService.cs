using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Portfolio.Application.Visitors;

namespace Portfolio.Infrastructure.Visitors;

/// <summary>Saves the visit counters periodically and once more when the host stops.</summary>
public sealed class VisitFlushService(IVisitTracker tracker, IOptions<VisitorOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.FlushInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await tracker.FlushAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down: StopAsync performs the last save.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await tracker.FlushAsync(cancellationToken);
    }
}
