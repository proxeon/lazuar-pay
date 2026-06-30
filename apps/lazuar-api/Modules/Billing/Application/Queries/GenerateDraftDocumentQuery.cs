using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Application.Queries;

public record GenerateDraftDocumentQuery(Guid OrganizationId, Guid SessionId) : IQuery<byte[]>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
