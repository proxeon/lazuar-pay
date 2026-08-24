using Lazuar.Pay.Data;

namespace Lazuar.Pay.Hosting;

internal static class HealthEndpoints
{
    public static void MapHealth(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/ready", async (PayDbContext db, CancellationToken ct) =>
        {
            try
            {
                await db.Database.CanConnectAsync(ct);
                return Results.Ok(new { status = "ready" });
            }
            catch
            {
                return Results.Json(new { status = "not_ready" }, statusCode: 503);
            }
        });
    }
}
