using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.Billing.Contracts;

public interface IBillingQueryService
{
    Task<PaginatedResponse<LedgerEntryDto>> GetLedgerEntriesAsync(Guid organizationId, int page, int limit, string? search, string? typeFilter, DateTime? fromDate, DateTime? toDate);
    Task<FinancialSummaryDto> GetFinancialSummaryAsync(Guid organizationId);
    Task<bool> HasPositiveCreditBalanceAsync(Guid organizationId);
    Task<int> GetAvailableCreditsAsync(Guid organizationId);
    Task<bool> HasSufficientCreditsAsync(Guid organizationId, int amount);
    Task<CreditBalanceDto> GetCreditBalanceWithHistoryAsync(Guid organizationId);
    Task<TenantBillingProfileDto?> GetBillingProfileAsync(Guid organizationId);
}
