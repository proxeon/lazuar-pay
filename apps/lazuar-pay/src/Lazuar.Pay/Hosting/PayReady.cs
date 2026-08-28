namespace Lazuar.Pay.Hosting;

internal static class PayReady
{
    public static IResult From(bool canConnect) =>
        canConnect
            ? Results.Ok(new { status = "ready" })
            : Results.Json(new { status = "not_ready" }, statusCode: 503);
}
