using System;
using BuildingBlocks.Domain;

namespace Modules.Commerce.Domain.Entities;

public class InvoiceReminderDispatchLog : Entity
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public int DayOffset { get; private set; }
    public DateTime DispatchedAt { get; private set; }

#pragma warning disable CS8618
    private InvoiceReminderDispatchLog() { }
#pragma warning restore CS8618

    public InvoiceReminderDispatchLog(Guid sessionId, int dayOffset)
    {
        Id = Guid.CreateVersion7();
        SessionId = sessionId;
        DayOffset = dayOffset;
        DispatchedAt = DateTime.UtcNow;
    }
}
