using System;
using BuildingBlocks.Domain;

namespace Modules.Billing.Domain.Aggregates;

public class DeferredRevenueSchedule : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public Guid LedgerEntryId { get; private set; }
    public decimal TotalDeferredAmount { get; private set; }
    public decimal RecognizedAmount { get; private set; }
    public string Currency { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public string Status { get; private set; }

#pragma warning disable CS8618
    private DeferredRevenueSchedule() { }
#pragma warning restore CS8618

    public DeferredRevenueSchedule(Guid organizationId, Guid ledgerEntryId, decimal totalDeferredAmount, string currency, DateTime startDate, DateTime endDate)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        LedgerEntryId = ledgerEntryId;
        TotalDeferredAmount = totalDeferredAmount;
        RecognizedAmount = 0;
        Currency = currency;
        StartDate = startDate;
        EndDate = endDate;
        Status = "PENDING";
    }

    public decimal Recognize(DateTime asOfDate)
    {
        if (asOfDate < StartDate || Status == "COMPLETED") return 0;

        var totalDays = (EndDate - StartDate).TotalDays;
        if (totalDays <= 0) return TotalDeferredAmount - RecognizedAmount;

        var elapsedDays = Math.Min((asOfDate - StartDate).TotalDays, totalDays);
        var totalShouldBeRecognized = (decimal)(elapsedDays / totalDays * (double)TotalDeferredAmount);
        var amountToRecognizeNow = totalShouldBeRecognized - RecognizedAmount;

        if (amountToRecognizeNow > 0)
        {
            RecognizedAmount += amountToRecognizeNow;
            Status = RecognizedAmount >= TotalDeferredAmount ? "COMPLETED" : "RECOGNIZING";
        }

        return amountToRecognizeNow;
    }
}
