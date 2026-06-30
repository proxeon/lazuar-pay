using System;
using BuildingBlocks.Domain;

namespace Modules.Billing.Domain.Entities;

public class DocumentSequence : Entity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Prefix { get; private set; }
    public long CurrentValue { get; private set; }

#pragma warning disable CS8618
    private DocumentSequence() { }
#pragma warning restore CS8618

    public DocumentSequence(Guid organizationId, string prefix, long startValue = 0)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Prefix = prefix;
        CurrentValue = startValue;
    }
}
