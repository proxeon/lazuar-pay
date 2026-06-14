using System;
using System.Threading.Tasks;
using Lazuar.ApiTypes;

namespace Modules.Billing.Contracts;

public interface IBillingQueryService
{
    Task<FinancialSummaryDto> GetFinancialSummaryAsync(Guid organizationId);
}
