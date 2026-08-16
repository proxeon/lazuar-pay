using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modules.Billing.Application.Queries;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure.Services;

namespace Modules.Billing.Infrastructure.Queries;

public class GetWorkspaceSaasQueryHandler : IQueryHandler<GetWorkspaceSaasQuery, WorkspaceSaasView>
{
    private readonly BillingDbContext _dbContext;
    private readonly SaasOptions _saas;

    public GetWorkspaceSaasQueryHandler(BillingDbContext dbContext, IOptions<SaasOptions> saas)
    {
        _dbContext = dbContext;
        _saas = saas.Value;
    }

    public async Task<WorkspaceSaasView> Handle(GetWorkspaceSaasQuery request, CancellationToken cancellationToken)
    {
        var row = await _dbContext.WorkspaceSaasSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId, cancellationToken);

        var plan = _saas.Plan;
        return new WorkspaceSaasView(
            request.OrganizationId,
            row?.Status ?? WorkspaceSaasStatuses.Unpaid,
            row?.CurrentPeriodStart,
            row?.CurrentPeriodEnd,
            row?.NextInvoiceAt,
            new SaasPlanView(
                plan.Code,
                plan.Name,
                plan.AmountMyr,
                plan.Interval,
                plan.Currency));
    }
}
