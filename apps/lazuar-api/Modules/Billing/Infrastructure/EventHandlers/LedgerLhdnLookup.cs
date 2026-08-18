using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;

namespace Modules.Billing.Infrastructure.EventHandlers;

internal static class LedgerLhdnLookup
{
    public static async Task<List<LedgerEntry>> MatchingAsync(
        IQueryable<LedgerEntry> entries,
        Guid organizationId,
        string internalId)
    {
        var matches = await entries
            .IgnoreQueryFilters()
            .Where(e => e.OrganizationId == organizationId
                && (e.CustomerDocumentNumber == internalId
                    || e.TaxInvoiceId == internalId
                    || e.ReferenceId == internalId
                    || e.LhdnDocumentUuid == internalId))
            .ToListAsync();

        return matches
            .OrderBy(e => e.ReferenceType == LedgerReferenceTypes.GatewayPayment ? 0 : 1)
            .ThenBy(e => e.Timestamp)
            .ToList();
    }
}
