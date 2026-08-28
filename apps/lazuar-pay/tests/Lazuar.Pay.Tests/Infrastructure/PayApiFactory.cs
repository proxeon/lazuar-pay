using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Lazuar.Pay.Identity.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Lazuar.Pay.Tests;

public sealed class PayApiFactory : WebApplicationFactory<Program>
{
    public FakeOneHandler One { get; } = new();
    public FakePspHandler Psp { get; } = new();
    readonly string _dbName = "pay-" + Guid.NewGuid().ToString("N");

    public string StripeWebhookSecret { get; init; } = "whsec_test_local";

    public string TestWebhookSecret { get; init; } = "test_whsec_local";

    public string OneWebhookSecret { get; init; } = "";

    public string PublicBaseUrl { get; init; } = "https://pay.test.example";

    public string? CorsOrigins { get; init; }

    public string EnvironmentName { get; init; } = "Testing";

    public int StartMaxPerMinute { get; init; } = 200;

    /// <summary>When set, tests run against Npgsql. InMemory is not a transaction proof — see PostgresTxTests.</summary>
    public string? PostgresConnection { get; init; }

    /// <summary>
    /// InMemory BeginTransaction is a no-op. ThrowNext proves the probe path only.
    /// ThrowAfterSave + PostgresConnection is the TX rollback proof.
    /// </summary>
    public FulfillmentProbe Probe { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("Pay:StripeWebhookSecret", StripeWebhookSecret);
        builder.UseSetting("Pay:TestWebhookSecret", TestWebhookSecret);
        builder.UseSetting("Pay:OneWebhookSecret", OneWebhookSecret);
        builder.UseSetting("Pay:PublicBaseUrl", PublicBaseUrl);
        builder.UseSetting("Pay:CheckoutBaseUrl", "http://pay-checkout.test.example");
        builder.UseSetting("Pay:StartMaxPerMinute", StartMaxPerMinute.ToString());
        if (!string.IsNullOrWhiteSpace(CorsOrigins))
        {
            builder.UseSetting("Pay:CorsOrigins", CorsOrigins);
        }
        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(s => s.ServiceType == typeof(OneClient)).ToList())
            {
                services.Remove(d);
            }

            foreach (var d in services.Where(s => s.ServiceType == typeof(IHttpClientFactory)).ToList())
            {
                services.Remove(d);
            }

            services.AddSingleton<IHttpClientFactory>(new StaticHttpFactory(Psp));
            foreach (var d in services.Where(s =>
                         s.ServiceType == typeof(PayDbContext)
                         || s.ServiceType == typeof(DbContextOptions<PayDbContext>)).ToList())
            {
                services.Remove(d);
            }

            if (!string.IsNullOrWhiteSpace(PostgresConnection))
            {
                var cs = PostgresConnection;
                services.AddDbContext<PayDbContext>(o => o.UseNpgsql(cs));
            }
            else
            {
                services.AddDbContext<PayDbContext>(o => o.UseInMemoryDatabase(_dbName)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            }
            services.AddSingleton(Probe);
            services.AddScoped<IFulfillPaid>(sp =>
                new ProbingFulfillment(sp.GetRequiredService<Fulfillment>(), Probe));
            services.AddTransient(_ =>
            {
                var http = new HttpClient(One, disposeHandler: false)
                {
                    BaseAddress = new Uri("http://one.test/api/v1/"),
                    Timeout = TimeSpan.FromSeconds(2)
                };
                return new OneClient(http, Options.Create(new OneOptions
                {
                    BaseUrl = "http://one.test/api/v1",
                    TimeoutSeconds = 2
                }));
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        if (!string.IsNullOrWhiteSpace(PostgresConnection))
        {
            db.Database.Migrate();
        }
        else
        {
            db.Database.EnsureCreated();
        }

        return host;
    }
}
