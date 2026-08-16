using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure.Services;
using Modules.One.Contracts;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Queries;

namespace Modules.Billing.Infrastructure.Commands;

public class CreateSaasCheckoutCommandHandler : ICommandHandler<CreateSaasCheckoutCommand, string>
{
    private readonly BillingDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly IOneQueryService _oneQueryService;
    private readonly SaasOptions _saas;

    public CreateSaasCheckoutCommandHandler(
        BillingDbContext dbContext,
        IMediator mediator,
        IOneQueryService oneQueryService,
        IOptions<SaasOptions> saas)
    {
        _dbContext = dbContext;
        _mediator = mediator;
        _oneQueryService = oneQueryService;
        _saas = saas.Value;
    }

    public async Task<string> Handle(CreateSaasCheckoutCommand request, CancellationToken cancellationToken)
    {
        if (request.OrganizationId == PlatformCheckoutTypes.SystemOrganizationId)
            throw new InvalidOperationException("System organization cannot subscribe to Hub.");

        if (string.IsNullOrWhiteSpace(request.ReturnUrl))
            throw new InvalidOperationException("return_url is required.");

        var plan = _saas.Plan;
        if (plan.AmountMyr <= 0)
            throw new InvalidOperationException("Hub plan price is not configured.");

        if (!SaasPlanInterval.IsValid(plan.Interval))
            throw new InvalidOperationException("Hub plan interval must be mo or yr.");

        if (!string.Equals(plan.Currency, "MYR", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Hub plan currency must be MYR.");

        var existing = await _dbContext.WorkspaceSaasSubscriptions
            .FirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId, cancellationToken);
        if (existing == null)
        {
            _dbContext.WorkspaceSaasSubscriptions.Add(
                new WorkspaceSaasSubscription(request.OrganizationId, plan.Code));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var customerEmail = await ResolveAdminEmailAsync(request.OrganizationId);
        var metadata = new Dictionary<string, string>
        {
            ["type"] = PlatformCheckoutTypes.PlatformSaasFee,
            ["tenant_id"] = request.OrganizationId.ToString(),
            ["plan_code"] = plan.Code
        };

        var query = new GenerateSystemCheckoutSessionQuery(
            request.OrganizationId,
            plan.AmountMyr,
            plan.Currency,
            SaasPlanInterval.ProductName(plan.Name, plan.Interval),
            customerEmail,
            request.ReturnUrl,
            request.ReturnUrl,
            metadata);

        return await _mediator.Send(query, cancellationToken);
    }

    private async Task<string> ResolveAdminEmailAsync(Guid organizationId)
    {
        var members = (await _oneQueryService.GetWorkspaceMembersAsync(organizationId)).ToList();
        var admin = members.FirstOrDefault(m =>
            string.Equals(m.Role, "ADMIN", StringComparison.OrdinalIgnoreCase));
        return admin?.Email ?? members.FirstOrDefault()?.Email ?? "";
    }
}
