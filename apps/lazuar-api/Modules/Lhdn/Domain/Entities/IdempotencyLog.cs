using System;
using BuildingBlocks.Domain;

namespace Modules.Lhdn.Domain.Entities;

/// <summary>
/// Guarantees that external SDK clients executing network retries do not 
/// accidentally duplicate submissions or deduct multiple wallet credits.
/// </summary>
public class IdempotencyLog : Entity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string IdempotencyKey { get; private set; }
    public int ResponseStatusCode { get; private set; }
    public string ResponseBody { get; private set; }
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private IdempotencyLog() { }
#pragma warning restore CS8618

    public IdempotencyLog(Guid organizationId, string idempotencyKey, int responseStatusCode, string responseBody)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        IdempotencyKey = idempotencyKey.Trim();
        ResponseStatusCode = responseStatusCode;
        ResponseBody = responseBody;
        CreatedAt = DateTime.UtcNow;
    }
}
