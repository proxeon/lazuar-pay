using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Contracts;
using Modules.Commerce.Application.Queries;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Infrastructure.Services;

public partial class CommerceQueryService : ICommerceQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IBillingQueryService? _billingQueryService;

    public CommerceQueryService(
        [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory connectionFactory,
        ICrmQueryService crmQueryService,
        IBillingQueryService? billingQueryService = null)
    {
        _connectionFactory = connectionFactory;
        _crmQueryService = crmQueryService;
        _billingQueryService = billingQueryService;
    }
}
