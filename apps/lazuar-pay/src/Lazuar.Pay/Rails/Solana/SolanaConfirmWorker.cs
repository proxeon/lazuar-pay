namespace Lazuar.Pay.Rails.Solana;

public sealed class SolanaConfirmWorker(IServiceScopeFactory scopes, ILogger<SolanaConfirmWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(2);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var confirm = scope.ServiceProvider.GetRequiredService<SolanaConfirm>();
                await confirm.ConfirmOpenByReferenceAsync(stoppingToken);
                delay = TimeSpan.FromSeconds(2);
            }
            catch (SolanaRpcThrottledException)
            {
                delay = TimeSpan.FromSeconds(15);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "solana confirm poller");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
