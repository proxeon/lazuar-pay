namespace BuildingBlocks.Application;

/// <summary>
/// Shared page/limit and limit/offset normalization for list endpoints.
/// Prefer <b>page + limit</b> for public/admin APIs; limit/offset is legacy-compatible (e.g. Ops chat).
/// </summary>
public static class Paging
{
    public const int DefaultPage = 1;
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    /// <summary>
    /// Normalize 1-based page + limit → (page, limit, skip).
    /// Invalid page → 1; limit outside 1..maxLimit → defaultLimit.
    /// </summary>
    public static (int Page, int Limit, int Skip) Normalize(
        int page,
        int limit,
        int defaultLimit = DefaultLimit,
        int maxLimit = MaxLimit)
    {
        var safePage = page < 1 ? DefaultPage : page;
        var safeLimit = limit < 1 || limit > maxLimit ? defaultLimit : limit;
        var skip = (safePage - 1) * safeLimit;
        return (safePage, safeLimit, skip);
    }

    /// <summary>
    /// Normalize limit + offset (0-based) → (limit, offset, currentPage).
    /// limit ≤ 0 → defaultLimit; offset &lt; 0 → 0; limit above max is clamped.
    /// </summary>
    public static (int Limit, int Offset, int CurrentPage) NormalizeOffset(
        int limit,
        int offset,
        int defaultLimit = 20,
        int maxLimit = MaxLimit)
    {
        var safeLimit = limit > 0 ? limit : defaultLimit;
        if (safeLimit > maxLimit)
            safeLimit = maxLimit;

        var safeOffset = offset < 0 ? 0 : offset;
        var currentPage = (safeOffset / safeLimit) + 1;
        return (safeLimit, safeOffset, currentPage);
    }
}
