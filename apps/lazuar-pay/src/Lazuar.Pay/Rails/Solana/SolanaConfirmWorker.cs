namespace Lazuar.Pay.Rails.Solana;

public sealed class SolanaConfirmWorker(IServiceScopeFactory scopes, ILogger<SolanaConfirmWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var confirm = scope.ServiceProvider.GetRequiredService<SolanaConfirm>();
                await confirm.ConfirmOpenByReferenceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "solana confirm poller");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
