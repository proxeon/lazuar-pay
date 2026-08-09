using Serilog;
using Microsoft.Extensions.Options;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.Configuration;
using BuildingBlocks.Infrastructure.Observability;
using Modules.One.Infrastructure.Configuration;
using Lazuar.Api;
using Lazuar.Api.Composition;
using Lazuar.Api.Jobs.ApiKeyMigration;
using Lazuar.Api.Jobs.WebhookSubscriptionMigration;
using Azure.Identity;
using Amazon.S3;
using Amazon.Runtime;
using Amazon;

var envPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../../../.env"));
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;
        var separatorIndex = trimmed.IndexOf('=');
        if (separatorIndex > 0)
        {
            var key = trimmed.Substring(0, separatorIndex).Trim();
            var value = trimmed.Substring(separatorIndex + 1).Trim();
            if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2) value = value.Substring(1, value.Length - 2);
            if (value.StartsWith("'") && value.EndsWith("'") && value.Length >= 2) value = value.Substring(1, value.Length - 2);
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

var keyVaultName = Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_NAME");
if (!string.IsNullOrEmpty(keyVaultName))
{
    try
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri($"https://{keyVaultName}.vault.azure.net/"),
            new DefaultAzureCredential());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WARNING] Failed to authenticate with Azure Key Vault: {ex.Message}. Falling back to local secrets.");
    }
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddOptions<BackgroundWorkerOptions>().BindConfiguration(BackgroundWorkerOptions.SectionName);
builder.Services.AddOptions<ObservabilityOptions>().BindConfiguration(ObservabilityOptions.SectionName);
builder.Services.AddOptions<PlatformAdminSettings>().Configure<IConfiguration>((settings, configuration) =>
{
    settings.Emails = configuration["PLATFORM_ADMIN_EMAILS"] ?? string.Empty;
    settings.Password = configuration["PLATFORM_ADMIN_PASSWORD"] ?? string.Empty;
});

// C.6 Observability: metrics collector + gauge refresh (outbox lag, dead letters, LHDN stuck)
var defaultConnectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Default connection string was not found.");
LazuarMetricsGauges.EnsureRegistered();
builder.Services.AddSingleton<IPlatformMetricsCollector>(sp =>
    new PlatformMetricsCollector(
        defaultConnectionString,
        sp.GetRequiredService<IOptions<ObservabilityOptions>>(),
        sp.GetServices<BuildingBlocks.Application.Observability.IOutboxSchemaRegistration>(),
        sp.GetServices<BuildingBlocks.Application.Observability.IPlatformMetricsContributor>(),
        sp.GetRequiredService<ILogger<PlatformMetricsCollector>>()));
builder.Services.AddHostedService<PlatformMetricsRefreshJob>();

// R03: optional one-shot legacy API key migrator (lhdn.DeveloperApiKeys → one.ApiCredentials).
// Dual-read middleware stays; only registers when Enabled=true.
builder.Services.AddOptions<ApiKeyMigrationOptions>()
    .BindConfiguration(ApiKeyMigrationOptions.SectionName)
    .PostConfigure(opts =>
    {
        var enabledEnv = Environment.GetEnvironmentVariable("API_KEY_MIGRATION_ENABLED");
        if (!string.IsNullOrWhiteSpace(enabledEnv) && bool.TryParse(enabledEnv, out var enabled))
        {
            opts.Enabled = enabled;
        }

        var dryRunEnv = Environment.GetEnvironmentVariable("API_KEY_MIGRATION_DRY_RUN");
        if (!string.IsNullOrWhiteSpace(dryRunEnv) && bool.TryParse(dryRunEnv, out var dryRun))
        {
            opts.DryRun = dryRun;
        }

        if (opts.BatchSize <= 0)
        {
            opts.BatchSize = 500;
        }
    });

{
    var migrationSection = builder.Configuration.GetSection(ApiKeyMigrationOptions.SectionName);
    var migrationEnabled = migrationSection.GetValue<bool?>(nameof(ApiKeyMigrationOptions.Enabled)) ?? false;
    var enabledEnv = Environment.GetEnvironmentVariable("API_KEY_MIGRATION_ENABLED");
    if (!string.IsNullOrWhiteSpace(enabledEnv) && bool.TryParse(enabledEnv, out var envEnabled))
    {
        migrationEnabled = envEnabled;
    }

    if (migrationEnabled)
    {
        builder.Services.AddSingleton<IApiKeyMigrationStore>(sp =>
            new SqlApiKeyMigrationStore(defaultConnectionString));
        builder.Services.AddSingleton<LegacyApiKeyMigrator>();
        builder.Services.AddHostedService<LegacyApiKeyMigrationHostedService>();
        Log.Information("API key migration hosted service registered (Enabled=true). Dual-read unchanged.");
    }
}

// R41: optional one-shot LHDN webhook registry backfill (lhdn.WebhookSubscriptions → one.TenantWebhookEndpoints).
// Lhdn fire-and-forget stays; only registers when Enabled=true. Dual-write of register API is out of scope.
builder.Services.AddOptions<WebhookSubscriptionMigrationOptions>()
    .BindConfiguration(WebhookSubscriptionMigrationOptions.SectionName)
    .PostConfigure(opts =>
    {
        var enabledEnv = Environment.GetEnvironmentVariable("WEBHOOK_SUBSCRIPTION_MIGRATION_ENABLED");
        if (!string.IsNullOrWhiteSpace(enabledEnv) && bool.TryParse(enabledEnv, out var enabled))
        {
            opts.Enabled = enabled;
        }

        var dryRunEnv = Environment.GetEnvironmentVariable("WEBHOOK_SUBSCRIPTION_MIGRATION_DRY_RUN");
        if (!string.IsNullOrWhiteSpace(dryRunEnv) && bool.TryParse(dryRunEnv, out var dryRun))
        {
            opts.DryRun = dryRun;
        }

        if (opts.BatchSize <= 0)
        {
            opts.BatchSize = 500;
        }
    });

{
    var webhookMigrationSection = builder.Configuration.GetSection(WebhookSubscriptionMigrationOptions.SectionName);
    var webhookMigrationEnabled = webhookMigrationSection.GetValue<bool?>(nameof(WebhookSubscriptionMigrationOptions.Enabled)) ?? false;
    var webhookEnabledEnv = Environment.GetEnvironmentVariable("WEBHOOK_SUBSCRIPTION_MIGRATION_ENABLED");
    if (!string.IsNullOrWhiteSpace(webhookEnabledEnv) && bool.TryParse(webhookEnabledEnv, out var webhookEnvEnabled))
    {
        webhookMigrationEnabled = webhookEnvEnabled;
    }

    if (webhookMigrationEnabled)
    {
        builder.Services.AddSingleton<IWebhookSubscriptionMigrationStore>(sp =>
            new SqlWebhookSubscriptionMigrationStore(defaultConnectionString));
        builder.Services.AddSingleton<LegacyWebhookSubscriptionMigrator>();
        builder.Services.AddHostedService<LegacyWebhookSubscriptionMigrationHostedService>();
        Log.Information("Webhook subscription migration hosted service registered (Enabled=true). Lhdn fire-and-forget unchanged.");
    }
}


builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IExecutionContextAccessor, ExecutionContextAccessor>();
builder.Services.AddSingleton<DatabaseJobTrigger>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<ISecretVault, AesSecretVault>();
builder.Services.AddSingleton<InMemoryEventBus>();
builder.Services.AddSingleton<IEventBusSubscriptions>(sp => sp.GetRequiredService<InMemoryEventBus>());

// R2 / S3 is optional at boot — production can run without object storage until keys are set.
AWSConfigsS3.UseSignatureVersion4 = true;
var r2Endpoint = builder.Configuration["R2_ENDPOINT"];
if (!string.IsNullOrWhiteSpace(r2Endpoint))
{
    var r2Config = new AmazonS3Config
    {
        ServiceURL = r2Endpoint,
        ForcePathStyle = true,
        AuthenticationRegion = "auto",
        SignatureVersion = "4"
    };

    var s3Credentials = new BasicAWSCredentials(
        builder.Configuration["R2_ACCESS_KEY"] ?? "",
        builder.Configuration["R2_SECRET_KEY"] ?? "");

    builder.Services.AddSingleton<IAmazonS3>(new AmazonS3Client(s3Credentials, r2Config));
    builder.Services.AddSingleton<IR2StorageService, R2StorageService>();
}
else
{
    Log.Warning("R2_ENDPOINT not set — object storage disabled (uploads will fail until configured).");
    builder.Services.AddSingleton<IR2StorageService, DisabledR2StorageService>();
}

builder.Services.AddTransient<Lazuar.Api.EventHandlers.ApiKeyRevokedIntegrationEventHandler>();
builder.Services.AddTransient<Lazuar.Api.EventHandlers.WorkspaceUpdatedIntegrationEventHandler>();

builder.Services.AddLazuarAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddLazuarAuthorizationPolicies();
builder.Services.AddLazuarCors(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddLazuarMediatR();
builder.Services.AddAllModules(builder.Configuration);

var app = builder.Build();

// First boot / empty Neon: apply EF migrations for every module schema before hosted services run.
await app.MigrateAllModuleDatabasesAsync();

app.UseLazuarPipeline();
app.UseAllModuleSubscriptions();
app.UseHostEventSubscriptions();
app.MapHealthEndpoints();
app.MapAllModuleEndpoints();

await app.RunAsync();

public partial class Program { }
