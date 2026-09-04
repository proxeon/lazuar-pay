using Microsoft.Extensions.Logging;

namespace Lazuar.Pay.Webhooks.Outbound;

internal sealed class OutboundWebhookWorker(
    IServiceScopeFactory scopes,
    ILogger<OutboundWebhookWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<OutboundWebhookDispatch>().ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // B2 (plans/023-evals/02): a batch failure must be visible —
                // swallowing it meant a wedged delivery pipeline was undetectable.
                logger.LogError(ex, "webhook dispatch pass failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
