// apps/lazuar-api/Modules/Billing/Contracts/IBillingQueryService.cs
using System;
using System.Threading.Tasks;
using Lazuar.ApiTypes;

namespace Modules.Billing.Contracts;

public interface IBillingQueryService
{
    Task<FinancialSummaryDto> GetFinancialSummaryAsync(Guid organizationId);
    Task<bool> HasPositiveCreditBalanceAsync(Guid organizationId);
    Task<CreditBalanceDto> GetCreditBalanceWithHistoryAsync(Guid organizationId);
}
