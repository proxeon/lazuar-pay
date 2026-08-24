namespace Lazuar.Pay.Hosting;

internal static class PayErrors
{
    public static IResult Status(int status, string title, string detail) =>
        Results.Json(new { status, title, detail }, statusCode: status);
}
