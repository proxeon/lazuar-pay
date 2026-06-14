using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Application.Ports;

public interface ILhdnRepository
{
    Task<LhdnTenantConfig?> GetTenantConfigAsync(Guid organizationId, CancellationToken ct = default);
    
    Task<TaxDocument?> GetTaxDocumentAsync(Guid id, CancellationToken ct = default);
    void AddTaxDocument(TaxDocument document);
    
    Task<IEnumerable<WebhookSubscription>> GetActiveWebhooksAsync(Guid organizationId, CancellationToken ct = default);
    void AddWebhookSubscription(WebhookSubscription subscription);
    
    void AddDeveloperApiKey(DeveloperApiKey key);

    Task SaveChangesAsync(CancellationToken ct = default);
}
