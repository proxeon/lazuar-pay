using System.Threading;
using System.Threading.Tasks;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Application.Services;

public interface IWebhookSenderService
{
    Task SendWebhookAsync(WebhookSubscription subscription, string payloadJson, CancellationToken ct = default);
}
