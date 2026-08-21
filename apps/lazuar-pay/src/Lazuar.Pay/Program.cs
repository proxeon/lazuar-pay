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
app.MapWhoami();
app.MapOrgReady();
app.MapCheckouts();

app.Run();

public partial class Program;
