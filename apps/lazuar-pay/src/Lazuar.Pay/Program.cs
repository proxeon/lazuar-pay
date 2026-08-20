using Lazuar.Pay.One;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOptions<OneOptions>().BindConfiguration(OneOptions.Section);
// Test seam: ConfigureTestServices re-registers OneClient with a fake HttpMessageHandler.
builder.Services.AddHttpClient<OneClient>();
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
