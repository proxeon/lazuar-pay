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
    Task SaveChangesAsync(CancellationToken ct = default);
}
