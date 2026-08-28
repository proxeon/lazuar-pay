namespace Lazuar.Pay.Hosting;

internal sealed class PayProblem
{
    public int Status { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
}

internal static class PayErrors
{
    public static IResult Status(int status, string title, string detail) =>
        Results.Json(new PayProblem { Status = status, Title = title, Detail = detail }, statusCode: status);

    public static bool TryForbiddenDetail(IResult result, out string detail)
    {
        if (result is IValueHttpResult { Value: PayProblem { Status: 403 } problem })
        {
            detail = problem.Detail;
            return true;
        }

        detail = "";
        return false;
    }
}
