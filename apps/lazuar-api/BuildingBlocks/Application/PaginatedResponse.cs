namespace BuildingBlocks.Application;

public class PaginatedResponse<T>
{
    public IEnumerable<T> Data { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }

    public PaginatedResponse(IEnumerable<T> data, int totalCount, int currentPage, int limit)
    {
        Data = data;
        TotalCount = totalCount;
        CurrentPage = currentPage;
        TotalPages = (int)Math.Ceiling(totalCount / (double)limit);
    }
}
