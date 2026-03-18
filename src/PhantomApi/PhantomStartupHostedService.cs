using Microsoft.Extensions.Hosting;

sealed class PhantomStartupHostedService : BackgroundService
{
    private readonly PhantomStartupWork _startupWork;

    public PhantomStartupHostedService(PhantomStartupWork startupWork)
    {
        _startupWork = startupWork;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _startupWork.PrimeWarmRuntimeAsync(stoppingToken);
        await _startupWork.WarmConfiguredEndpointsAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_startupWork.AppServerClient is not null)
        {
            await _startupWork.AppServerClient.DisposeAsync();
        }
    }
}
