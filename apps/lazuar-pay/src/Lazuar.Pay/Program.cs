using System.Text.Json;
using Lazuar.Pay.Checkouts;
using Lazuar.Pay.One;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.AddOptions<OneOptions>().BindConfiguration(OneOptions.Section);
// Test seam: ConfigureTestServices re-registers OneClient with a fake HttpMessageHandler.
builder.Services.AddHttpClient<OneClient>();
builder.Services.AddSingleton<CheckoutStore>();
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));
app.MapWhoami();
app.MapOrgReady();
app.MapCheckouts();

app.Run();

public partial class Program;
