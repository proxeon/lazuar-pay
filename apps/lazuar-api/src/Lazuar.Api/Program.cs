using Serilog;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.IO;
using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.Configuration;
using BuildingBlocks.Infrastructure.Llm;
using BuildingBlocks.Infrastructure.Observability;
using Modules.One.Infrastructure;
using Modules.One.Infrastructure.Configuration;
using Modules.Messaging.Infrastructure;
using Modules.CRM.Infrastructure;
using Modules.Payments.Infrastructure;
using Modules.Ops.Infrastructure;
using Modules.Billing.Infrastructure;
using Modules.Lhdn.Infrastructure;
using Modules.Commerce.Infrastructure;
using Modules.Communications.Infrastructure;
using Lazuar.Api;
using Lazuar.Api.Middleware;
using Lazuar.ApiTypes;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
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

builder.Services.AddOptions<ResendOptions>().BindConfiguration(ResendOptions.SectionName);
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
        sp.GetRequiredService<ILogger<PlatformMetricsCollector>>()));
builder.Services.AddHostedService<PlatformMetricsRefreshJob>();

builder.Services.AddHttpClient("Resend", (sp, client) =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
    var options = sp.GetRequiredService<IOptions<ResendOptions>>().Value;
    if (!string.IsNullOrEmpty(options.ApiKey))
    {
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
    }
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IExecutionContextAccessor, ExecutionContextAccessor>();
builder.Services.AddSingleton<DatabaseJobTrigger>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IMessagingService, ConsoleMessagingService>();
builder.Services.AddSingleton<IEmailService, ResendEmailService>();
builder.Services.AddSingleton<ISecretVault, AesSecretVault>();
builder.Services.AddSingleton<IMagicLinkTokenService, MagicLinkTokenService>();
builder.Services.AddThinLlmFactory();
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

const string defaultDevJwtSecret = "secure_development_key_minimum_32_characters_long";
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (builder.Environment.IsProduction())
{
    if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret == defaultDevJwtSecret)
    {
        throw new InvalidOperationException(
            "Jwt:Secret must be configured to a non-default value in Production.");
    }
}

var guardedJwtSecret = string.IsNullOrWhiteSpace(jwtSecret) ? defaultDevJwtSecret : jwtSecret;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "lazuar-api",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "lazuar-clients",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(guardedJwtSecret))
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var isPlatformRoute = context.Request.Path.StartsWithSegments("/api/v1/platform");
            var cookieName = isPlatformRoute ? "lazuar_admin_auth" : "lazuar_auth";

            if (context.Request.Cookies.TryGetValue(cookieName, out var token))
            {
                context.Token = token;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    // Human org admins only — key mint/revoke, certs, payment/email config, member admin.
    options.AddPolicy("OrgAdmin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("SUPER_ADMIN", "ADMIN");
    });

    // LHDN document write (submit / cancel): human admins bypass; API_CLIENT needs write scope.
    options.AddPolicy("IntegrationLhdnDocumentsWrite", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("SUPER_ADMIN")
            || ctx.User.IsInRole("ADMIN")
            || (ctx.User.IsInRole("API_CLIENT")
                && ctx.User.HasClaim("scope", Modules.One.Domain.PlatformApiScopes.LhdnDocumentsWrite)));
    });

    // LHDN document read (GET status): human admins bypass; API_CLIENT needs read or write (write implies read).
    options.AddPolicy("IntegrationLhdnDocumentsRead", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("SUPER_ADMIN")
            || ctx.User.IsInRole("ADMIN")
            || (ctx.User.IsInRole("API_CLIENT")
                && (ctx.User.HasClaim("scope", Modules.One.Domain.PlatformApiScopes.LhdnDocumentsRead)
                    || ctx.User.HasClaim("scope", Modules.One.Domain.PlatformApiScopes.LhdnDocumentsWrite))));
    });

    // Payments checkouts write (M2M ad-hoc checkout create — Phase 2 routes attach this policy).
    options.AddPolicy("IntegrationPaymentsCheckoutsWrite", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("SUPER_ADMIN")
            || ctx.User.IsInRole("ADMIN")
            || (ctx.User.IsInRole("API_CLIENT")
                && ctx.User.HasClaim("scope", Modules.One.Domain.PlatformApiScopes.PaymentsCheckoutsWrite)));
    });

    // Payments checkouts read (poll status): write implies read.
    options.AddPolicy("IntegrationPaymentsCheckoutsRead", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("SUPER_ADMIN")
            || ctx.User.IsInRole("ADMIN")
            || (ctx.User.IsInRole("API_CLIENT")
                && (ctx.User.HasClaim("scope", Modules.One.Domain.PlatformApiScopes.PaymentsCheckoutsRead)
                    || ctx.User.HasClaim("scope", Modules.One.Domain.PlatformApiScopes.PaymentsCheckoutsWrite))));
    });

    // Optional: payment connection status (no secrets). Do NOT attach to payment-config write.
    options.AddPolicy("IntegrationPaymentsConfigRead", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("SUPER_ADMIN")
            || ctx.User.IsInRole("ADMIN")
            || (ctx.User.IsInRole("API_CLIENT")
                && ctx.User.HasClaim("scope", Modules.One.Domain.PlatformApiScopes.PaymentsConfigRead)));
    });

    // Optional: manage outbound webhook endpoints via API (console/OrgAdmin remains primary v1).
    options.AddPolicy("IntegrationWebhooksEndpointsManage", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("SUPER_ADMIN")
            || ctx.User.IsInRole("ADMIN")
            || (ctx.User.IsInRole("API_CLIENT")
                && ctx.User.HasClaim("scope", Modules.One.Domain.PlatformApiScopes.WebhooksEndpointsManage)));
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var corsOrigins = builder.Configuration["App:CorsOrigins"];
        if (!string.IsNullOrEmpty(corsOrigins))
        {
            var origins = corsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.One.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Messaging.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Payments.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Ops.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Billing.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Lhdn.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Commerce.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Communications.Application.DependencyInjection).Assembly);
    
    cfg.RegisterServicesFromAssembly(typeof(Modules.One.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Messaging.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Payments.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.CRM.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Ops.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Billing.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Lhdn.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Commerce.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Communications.Infrastructure.DependencyInjection).Assembly);
});

builder.Services.AddOneModule(builder.Configuration);
builder.Services.AddMessagingModule(builder.Configuration);
builder.Services.AddCrmModule(builder.Configuration);
builder.Services.AddPaymentsModule(builder.Configuration);
builder.Services.AddOpsModule(builder.Configuration);
builder.Services.AddBillingModule(builder.Configuration);
builder.Services.AddLhdnModule(builder.Configuration);
builder.Services.AddCommerceModule(builder.Configuration);
builder.Services.AddCommunicationsModule(builder.Configuration);

var app = builder.Build();

// First boot / empty Neon: apply EF migrations for every module schema before hosted services run.
await using (var scope = app.Services.CreateAsyncScope())
{
    var sp = scope.ServiceProvider;
    var migratorLog = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigrator");
    DbContext[] contexts =
    [
        sp.GetRequiredService<OneDbContext>(),
        sp.GetRequiredService<MessagingDbContext>(),
        sp.GetRequiredService<PaymentsDbContext>(),
        sp.GetRequiredService<CrmDbContext>(),
        sp.GetRequiredService<OpsDbContext>(),
        sp.GetRequiredService<BillingDbContext>(),
        sp.GetRequiredService<LhdnDbContext>(),
        sp.GetRequiredService<CommerceDbContext>(),
        sp.GetRequiredService<CommunicationsDbContext>(),
    ];

    foreach (var ctx in contexts)
    {
        var name = ctx.GetType().Name;
        migratorLog.LogInformation("Applying EF migrations for {DbContext}...", name);
        try
        {
            await ctx.Database.MigrateAsync();
            migratorLog.LogInformation("Migrations applied for {DbContext}", name);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("PendingModelChanges", StringComparison.Ordinal))
        {
            // Should be rare after ConfigureWarnings(Ignore PendingModelChanges). Log and continue boot;
            // operator must add a migration for that module.
            migratorLog.LogError(ex,
                "MigrateAsync blocked for {DbContext} by pending model changes. Module tables may be missing.", name);
        }
        catch (Exception ex)
        {
            migratorLog.LogError(ex, "MigrateAsync failed for {DbContext}", name);
            throw;
        }
    }
}

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseMiddleware<TenantSecurityMiddleware>();
app.UseAuthorization();

app.UseOneSubscriptions();
app.UseMessagingSubscriptions();
app.UseCrmSubscriptions();
app.UsePaymentsSubscriptions();
app.UseOpsSubscriptions();
app.UseBillingSubscriptions();
app.UseLhdnSubscriptions();
app.UseCommerceSubscriptions();
app.UseCommunicationsSubscriptions();

var eventBus = app.Services.GetRequiredService<IEventBusSubscriptions>();
// Dual-subscribe: platform (One) credentials + legacy Lhdn keys during dual-read window
// (allowed until 2026-11-30; target One-only revoke event by 2026-12-15 — decisions 00.1).
// Do not drop the Lhdn subscription before cutover; see plans/004-maintenance/api-key-cutover-design.md.
eventBus.Subscribe<Modules.One.Contracts.Events.ApiKeyRevokedIntegrationEvent, Lazuar.Api.EventHandlers.ApiKeyRevokedIntegrationEventHandler>();
eventBus.Subscribe<Modules.Lhdn.Contracts.Events.ApiKeyRevokedIntegrationEvent, Lazuar.Api.EventHandlers.ApiKeyRevokedIntegrationEventHandler>();
eventBus.Subscribe<Modules.One.Contracts.WorkspaceUpdatedIntegrationEvent, Lazuar.Api.EventHandlers.WorkspaceUpdatedIntegrationEventHandler>();

// Liveness for deploy health-gates / Caddy (no auth, no CORS requirement)
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Readiness: DB connectivity; optional outbox lag threshold (Observability:OutboxLagReadyThreshold)
app.MapGet("/health/ready", async (
    IPlatformMetricsCollector collector,
    IOptions<ObservabilityOptions> observabilityOptions,
    CancellationToken ct) =>
{
    var result = await HealthReadiness.EvaluateAsync(collector, observabilityOptions, ct);
    var body = new
    {
        status = result.Status,
        database = result.DatabaseReachable ? "up" : "down",
        outbox_lag_seconds = result.OutboxLagSeconds,
        reason = result.Reason
    };
    return result.IsReady
        ? Results.Ok(body)
        : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
});

// Lightweight metrics snapshot (process counters + on-demand DB gauges)
app.MapGet("/health/metrics", async (IPlatformMetricsCollector collector, CancellationToken ct) =>
{
    var snapshot = await collector.CollectAsync(ct);
    return Results.Ok(new
    {
        collected_at_utc = snapshot.CollectedAtUtc,
        database_reachable = snapshot.DatabaseReachable,
        error = snapshot.Error,
        outbox_lag_seconds = snapshot.OutboxLagSeconds,
        outbox_pending_count = snapshot.OutboxPendingCount,
        dead_letter_count = snapshot.DeadLetterCount,
        lhdn_stuck_count = snapshot.LhdnStuckCount,
        counters = new
        {
            dead_letters_since_start = snapshot.DeadLettersSinceStart,
            webhook_failed_since_start = snapshot.WebhookFailedSinceStart,
            dunning_cancels_since_start = snapshot.DunningCancelsSinceStart
        },
        schemas = snapshot.Schemas
    });
});

var apiGroup = app.MapGroup("/api/v1").RequireCors();

apiGroup.MapOneEndpoints();
apiGroup.MapMessagingEndpoints();
apiGroup.MapPaymentsEndpoints();
apiGroup.MapPaymentsIntegrationEndpoints();
apiGroup.MapOpsEndpoints();
apiGroup.MapBillingEndpoints();
apiGroup.MapLhdnEndpoints();
apiGroup.MapCommerceEndpoints();
apiGroup.MapCommunicationsEndpoints();

var platformGroup = app.MapGroup("/api/v1/platform")
   .RequireCors()
   .RequireAuthorization(policy => policy.RequireRole("SUPER_ADMIN"));

platformGroup.MapPlatformEndpoints();

await app.RunAsync();

public partial class Program { }
