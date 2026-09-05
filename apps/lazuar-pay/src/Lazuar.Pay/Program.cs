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
using Lazuar.Pay.Subscriptions;
using Lazuar.Pay.Rails;
using Lazuar.Pay.Rails.Billplz;
using Lazuar.Pay.Rails.Chip;
using Lazuar.Pay.Rails.Razorpay;
using Lazuar.Pay.Rails.Solana;
using Lazuar.Pay.Rails.Stripe;
using Lazuar.Pay.Rails.Test;
using Lazuar.Pay.Rails.Xendit;
using Lazuar.Pay.Secrets;
using Lazuar.Pay.Webhooks;
using Lazuar.Pay.Webhooks.Outbound;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = false;
        o.TimestampFormat = "O";
        o.UseUtcTimestamp = true;
    });
}

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<OneWhoamiCache>();
builder.Services.AddOptions<OneOptions>().BindConfiguration(OneOptions.Section);
builder.Services.AddHttpClient<OneClient>();
builder.Services.AddHttpClient<OneWorkerClient>();
builder.Services.AddHttpClient("chip");
builder.Services.AddHttpClient("billplz");
builder.Services.AddHttpClient("xendit");
builder.Services.AddHttpClient("razorpay");
builder.Services.AddHttpClient("solana", c => c.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
// Issue 017 (issues/001): outbound webhook connections are pinned to connect-time-validated
// addresses. Validating the DNS answer in the dispatcher and then letting HttpClient
// re-resolve the hostname was a DNS-rebinding TOCTOU — the checked answer and the dialed
// answer could differ, pointing signed payment payloads at internal addresses. Loopback
// stays dialable in Development/Testing (same rule as OutboundUrl.AllowsLoopback).
var webhookAllowLoopback = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
builder.Services.AddHttpClient("pay-webhooks", c => c.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        ConnectCallback = async (context, ct) =>
        {
            System.IO.Stream stream = await Lazuar.Pay.Webhooks.Outbound.OutboundUrl.ConnectValidatedAsync(
                context.DnsEndPoint, webhookAllowLoopback, ct);
            return stream;
        }
    });
builder.Services.AddScoped<Lazuar.Pay.Webhooks.Outbound.OutboundWebhookDispatch>();
builder.Services.AddScoped<Lazuar.Pay.Money.RefundSettler>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<Lazuar.Pay.Webhooks.Outbound.OutboundWebhookWorker>();
    builder.Services.AddHostedService<Lazuar.Pay.Money.RefundSettleWorker>();
    builder.Services.AddHostedService<Lazuar.Pay.Rails.Solana.SolanaConfirmWorker>();
}
builder.Services.AddDataProtection();
builder.Services.AddSingleton<SecretBox>();
builder.Services.AddScoped<CheckoutStore>();
builder.Services.AddScoped<StripeHosted>();
builder.Services.AddScoped<ChipHosted>();
builder.Services.AddScoped<BillplzHosted>();
builder.Services.AddScoped<XenditHosted>();
builder.Services.AddScoped<RazorpayHosted>();
builder.Services.AddScoped<SolanaHosted>();
builder.Services.AddScoped<SolanaRpc>();
builder.Services.AddScoped<SolanaConfirm>();
builder.Services.AddScoped<TestHosted>();
builder.Services.AddScoped<ProcessorRemote>();
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
OneWorkerClient.ThrowIfInvalid(builder.Configuration);
var app = builder.Build();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    await PayBoot.ProbeSolanaRpcAsync(app.Services, app.Configuration, default);
}
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PayDbContext>().Database.MigrateAsync();
    }
    catch (Exception)
    {
        app.Logger.LogError("pay-db schema mismatch; run task pay:db:migrate");
    }
}

app.UseCors();
app.UsePayRequestLog();

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
app.MapRefunds();
app.MapSubscriptions();
app.MapOneWebhooks();
app.MapOrgWebhooks();

app.Run();

public partial class Program;
