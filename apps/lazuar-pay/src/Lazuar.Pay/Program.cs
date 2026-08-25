using System.Text.Json;
using Lazuar.Pay.Catalog;
using Lazuar.Pay.Checkouts;
using Lazuar.Pay.Credentials;
using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity;
using Lazuar.Pay.Identity.Client;
using Lazuar.Pay.Identity.OneWebhooks;
using Lazuar.Pay.Money;
using Lazuar.Pay.Money.Queries;
using Lazuar.Pay.PublicPay;
using Lazuar.Pay.Rails.Billplz;
using Lazuar.Pay.Rails.Chip;
using Lazuar.Pay.Rails.Razorpay;
using Lazuar.Pay.Rails.Stripe;
using Lazuar.Pay.Rails.Test;
using Lazuar.Pay.Rails.Xendit;
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
builder.Services.AddHttpClient("chip");
builder.Services.AddHttpClient("billplz");
builder.Services.AddHttpClient("xendit");
builder.Services.AddHttpClient("razorpay");
builder.Services.AddDataProtection();
builder.Services.AddSingleton<SecretBox>();
builder.Services.AddScoped<CheckoutStore>();
builder.Services.AddScoped<StripeHosted>();
builder.Services.AddScoped<ChipHosted>();
builder.Services.AddScoped<BillplzHosted>();
builder.Services.AddScoped<XenditHosted>();
builder.Services.AddScoped<RazorpayHosted>();
builder.Services.AddScoped<TestHosted>();
builder.Services.AddScoped<Fulfillment>();
builder.Services.AddScoped<IFulfillPaid>(sp => sp.GetRequiredService<Fulfillment>());
if (!builder.Environment.IsEnvironment("Testing"))
{
    var payCs = builder.Configuration.GetConnectionString("Pay");
    if (string.IsNullOrWhiteSpace(payCs)
        || payCs.IndexOf("Password=", StringComparison.OrdinalIgnoreCase) < 0)
    {
        payCs = "Host=localhost;Port=5435;Database=lazuar_pay;Username=postgres;Password=postgres";
    }

    builder.Services.AddDbContext<PayDbContext>(o => o.UseNpgsql(payCs));
}
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.WithOrigins(
                "http://localhost:5178",
                "http://127.0.0.1:5178",
                "http://localhost:5179",
                "http://127.0.0.1:5179",
                "http://localhost:4178",
                "http://127.0.0.1:4178",
                "http://localhost:4179",
                "http://127.0.0.1:4179")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
var app = builder.Build();
app.UseCors();

app.MapHealth();
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
