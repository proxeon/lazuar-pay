using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Lazuar.Pay.Identity.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Lazuar.Pay.Tests;

public sealed class PayApiFactory : WebApplicationFactory<Program>
{
    public FakeOneHandler One { get; } = new();
    public FakePspHandler Psp { get; } = new();

    string? _connectionString;
    bool _ownsDatabase;

    public string StripeWebhookSecret { get; init; } = "whsec_test_local";

    public string TestWebhookSecret { get; init; } = "test_whsec_local";

    public string OneWebhookSecret { get; init; } = "";

    public string? OneApiKey { get; init; }

    public string? OneWorkerOrgId { get; init; }

    public string PublicBaseUrl { get; init; } = "https://pay.test.example";

    public string? CorsOrigins { get; init; }

    public string EnvironmentName { get; init; } = "Testing";

    public int StartMaxPerMinute { get; init; } = 200;

    /// <summary>
    /// Optional override. Default is a unique database cloned from the suite template
    /// (<see cref="PayPostgres"/>). ThrowAfterSave is a TX rollback proof on that database.
    /// </summary>
    public string? PostgresConnection { get; init; }

    public FulfillmentProbe Probe { get; } = new();

    string ConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(PostgresConnection))
        {
            return PostgresConnection;
        }

        if (_connectionString is null)
        {
            _connectionString = PayPostgres.CreateDatabase();
            _ownsDatabase = true;
        }

        return _connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("Pay:StripeWebhookSecret", StripeWebhookSecret);
        builder.UseSetting("Pay:TestWebhookSecret", TestWebhookSecret);
        builder.UseSetting("Pay:OneWebhookSecret", OneWebhookSecret);
        builder.UseSetting("Pay:PublicBaseUrl", PublicBaseUrl);
        builder.UseSetting("Pay:CheckoutBaseUrl", "http://pay-checkout.test.example");
        builder.UseSetting("Pay:StartMaxPerMinute", StartMaxPerMinute.ToString());
        builder.UseSetting("Pay:Solana:RpcUrl", "http://solana.test/");
        builder.UseSetting("Pay:Solana:Cluster", "devnet");
        if (!string.IsNullOrWhiteSpace(OneApiKey))
        {
            builder.UseSetting("One:ApiKey", OneApiKey);
        }

        if (!string.IsNullOrWhiteSpace(OneWorkerOrgId))
        {
            builder.UseSetting("One:WorkerOrgId", OneWorkerOrgId);
        }
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

            var cs = ConnectionString();
            services.AddDbContext<PayDbContext>(o => o.UseNpgsql(cs));
            services.AddSingleton(Probe);
            services.AddScoped<IFulfillPaid>(sp =>
                new ProbingFulfillment(sp.GetRequiredService<Fulfillment>(), Probe));
            services.AddMemoryCache();
            services.AddSingleton<OneWhoamiCache>();
            services.AddTransient(sp =>
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
                }), sp.GetRequiredService<OneWhoamiCache>());
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Schema comes from the pay_template clone. Do not Migrate per test.
        _ = ConnectionString();
        return base.CreateHost(builder);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_ownsDatabase && _connectionString is not null)
        {
            PayPostgres.DropDatabase(_connectionString);
            _ownsDatabase = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _ownsDatabase && _connectionString is not null)
        {
            PayPostgres.DropDatabase(_connectionString);
            _ownsDatabase = false;
        }
    }
}
