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
using Lazuar.Pay.PaymentLinks;
using Lazuar.Pay.PublicPay;
using Lazuar.Pay.Rails.Billplz;
using Lazuar.Pay.Rails.Chip;
using Lazuar.Pay.Rails.Razorpay;
using Lazuar.Pay.Rails.Stripe;
using Lazuar.Pay.Rails.Test;
using Lazuar.Pay.Rails.Xendit;
using Lazuar.Pay.Secrets;
using Lazuar.Pay.Webhooks;
using Lazuar.Pay.Webhooks.Outbound;
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
builder.Services.AddHttpClient("pay-webhooks", c => c.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<Lazuar.Pay.Webhooks.Outbound.OutboundWebhookDispatch>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<Lazuar.Pay.Webhooks.Outbound.OutboundWebhookWorker>();
}
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
    if (string.IsNullOrWhiteSpace(payCs))
    {
        if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("ConnectionStrings:Pay is required");
        }

        payCs = "Host=localhost;Port=5435;Database=lazuar_pay;Username=postgres;Password=postgres";
    }

    builder.Services.AddDbContext<PayDbContext>(o => o.UseNpgsql(payCs));
}
PayCors.Add(builder);
PayBoot.ThrowIfMisconfigured(builder.Configuration, builder.Environment);
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PayDbContext>().Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "pay-db schema mismatch; run task pay:db:migrate");
    }
}

app.UseCors();

app.MapHealth();
app.MapWhoami();
app.MapOrgReady();
app.MapCheckouts();
app.MapPaymentLinks();
app.MapCatalog();
app.MapPublicPay();
app.MapGateways();
app.MapWebhooks();
app.MapPaymentQueries();
app.MapOneWebhooks();
app.MapOrgWebhooks();

app.Run();

public partial class Program;
