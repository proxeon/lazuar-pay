using System.Text.Json;
using Lazuar.Pay.Catalog;
using Lazuar.Pay.Checkouts;
using Lazuar.Pay.Data;
using Lazuar.Pay.Gateways;
using Lazuar.Pay.Money;
using Lazuar.Pay.One;
using Lazuar.Pay.PublicPay;
using Lazuar.Pay.Secrets;
using Lazuar.Pay.Webhooks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.AddOptions<OneOptions>().BindConfiguration(OneOptions.Section);
builder.Services.AddHttpClient<OneClient>();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<SecretBox>();
builder.Services.AddScoped<CheckoutStore>();
builder.Services.AddScoped<StripeHosted>();
builder.Services.AddScoped<Fulfillment>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    var payCs = builder.Configuration.GetConnectionString("Pay")
        ?? "Host=localhost;Port=5435;Database=lazuar_pay;Username=postgres;Password=postgres";
    builder.Services.AddDbContext<PayDbContext>(o => o.UseNpgsql(payCs));
}
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.WithOrigins(
                "http://localhost:5178",
                "http://127.0.0.1:5178",
                "http://localhost:5179",
                "http://127.0.0.1:5179")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
var app = builder.Build();
app.UseCors();

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
app.MapWhoami();
app.MapOrgReady();
app.MapCheckouts();
app.MapCatalog();
app.MapPublicPay();
app.MapGateways();
app.MapWebhooks();
app.MapPaymentQueries();
app.MapOneWebhooks();

app.Run();

public partial class Program;
