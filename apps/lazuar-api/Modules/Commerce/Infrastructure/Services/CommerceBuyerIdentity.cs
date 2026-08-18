using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Modules.Commerce.Contracts;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Infrastructure.Services;

public class CommerceBuyerIdentity : ICommerceBuyerIdentity
{
    private readonly IMediator _mediator;

    public CommerceBuyerIdentity(IMediator mediator)
    {
        _mediator = mediator;
    }

    public Task AttachTinAsync(
        Guid organizationId,
        string fullName,
        string email,
        string tin,
        string idType,
        string idValue,
        string companyName,
        CancellationToken ct = default) =>
        _mediator.Send(new ResolveClientProfileCommand(
            organizationId,
            fullName,
            email,
            Phone: "",
            Tin: tin,
            IdType: idType,
            IdValue: idValue,
            CompanyName: companyName), ct);
}
