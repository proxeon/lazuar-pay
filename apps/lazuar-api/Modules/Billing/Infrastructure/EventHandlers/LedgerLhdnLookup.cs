using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Domain.Aggregates;

namespace Modules.Billing.Infrastructure.EventHandlers;

internal static class LedgerLhdnLookup
{
    public static Task<List<LedgerEntry>> MatchingAsync(
        IQueryable<LedgerEntry> entries,
        Guid organizationId,
        string internalId) =>
        entries
            .IgnoreQueryFilters()
            .Where(e => e.OrganizationId == organizationId
                && (e.CustomerDocumentNumber == internalId
                    || e.TaxInvoiceId == internalId
                    || e.ReferenceId == internalId))
            .ToListAsync();
}
