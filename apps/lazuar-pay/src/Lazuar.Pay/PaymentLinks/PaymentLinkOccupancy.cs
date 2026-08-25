namespace Lazuar.Pay.PaymentLinks;

internal static class PaymentLinkOccupancy
{
    public static bool CountsTowardCapacity(string status) =>
        status is "open" or "paid";

    public static bool IsFull(int? maxPayers, int taken) =>
        maxPayers is int max && taken >= max;

    public static int? Remaining(int? maxPayers, int taken) =>
        maxPayers is int max ? Math.Max(0, max - taken) : null;
}
