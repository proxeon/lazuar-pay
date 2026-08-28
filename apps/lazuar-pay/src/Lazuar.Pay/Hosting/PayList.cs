namespace Lazuar.Pay.Hosting;

internal static class PayList
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public static int Clamp(int? limit)
    {
        if (limit is null or < 1)
        {
            return DefaultLimit;
        }

        return Math.Min(limit.Value, MaxLimit);
    }

    public static object Page<T>(IReadOnlyList<T> fetched, int limit, Func<T, string> id)
    {
        string? next = null;
        IReadOnlyList<T> items = fetched;
        if (fetched.Count > limit)
        {
            var page = fetched.Take(limit).ToList();
            items = page;
            next = id(page[^1]);
        }

        return new { items, next_cursor = next };
    }
}
