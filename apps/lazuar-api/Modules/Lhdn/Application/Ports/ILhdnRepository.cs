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
    Task<TaxDocument?> GetTaxDocumentByInternalIdAsync(Guid organizationId, string internalReferenceId, CancellationToken ct = default);

    void AddTaxDocument(TaxDocument document);
    
    Task<IEnumerable<WebhookSubscription>> GetActiveWebhooksAsync(Guid organizationId, CancellationToken ct = default);
    void AddWebhookSubscription(WebhookSubscription subscription);
    
    Task<DeveloperApiKey?> GetDeveloperApiKeyAsync(Guid id, CancellationToken ct = default);
    void AddDeveloperApiKey(DeveloperApiKey key);

    Task SaveChangesAsync(CancellationToken ct = default);
}
