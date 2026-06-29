using Serilog;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
using Modules.One.Infrastructure;
using Modules.One.Infrastructure.Configuration;
using Modules.Messaging.Infrastructure;
using Modules.Community.Infrastructure;
using Modules.CRM.Infrastructure;
using Modules.Payments.Infrastructure;
using Modules.Ops.Infrastructure;
using Modules.Billing.Infrastructure;
using Modules.Lhdn.Infrastructure;
using Modules.Commerce.Infrastructure;
using Modules.Vault.Infrastructure;
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
builder.Services.AddOptions<PlatformAdminSettings>().Configure<IConfiguration>((settings, configuration) =>
{
    settings.Emails = configuration["PLATFORM_ADMIN_EMAILS"] ?? string.Empty;
    settings.Password = configuration["PLATFORM_ADMIN_PASSWORD"] ?? string.Empty;
});

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
builder.Services.AddSingleton<IMagicLinkTokenService, MagicLinkTokenService>();
builder.Services.AddThinLlmFactory();
builder.Services.AddSingleton<InMemoryEventBus>();
builder.Services.AddSingleton<IEventBusSubscriptions>(sp => sp.GetRequiredService<InMemoryEventBus>());

AWSConfigsS3.UseSignatureVersion4 = true;

var r2Config = new AmazonS3Config
{
    ServiceURL = builder.Configuration["R2_ENDPOINT"],
    ForcePathStyle = true,
    AuthenticationRegion = "auto", 
    SignatureVersion = "4"
};

var s3Credentials = new BasicAWSCredentials(
    builder.Configuration["R2_ACCESS_KEY"] ?? "",
    builder.Configuration["R2_SECRET_KEY"] ?? "");

builder.Services.AddSingleton<IAmazonS3>(new AmazonS3Client(s3Credentials, r2Config));
builder.Services.AddSingleton<IR2StorageService, R2StorageService>();

builder.Services.AddTransient<Lazuar.Api.EventHandlers.ApiKeyRevokedIntegrationEventHandler>();
builder.Services.AddTransient<Lazuar.Api.EventHandlers.WorkspaceUpdatedIntegrationEventHandler>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var secret = builder.Configuration["Jwt:Secret"] ?? "secure_development_key_minimum_32_characters_long";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "lazuar-api",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "lazuar-clients",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
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
    options.AddPolicy("OrgAdmin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("SUPER_ADMIN", "ADMIN", "API_CLIENT");
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
    cfg.RegisterServicesFromAssembly(typeof(Modules.Community.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Payments.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Ops.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Billing.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Lhdn.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Commerce.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Vault.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Communications.Application.DependencyInjection).Assembly);
    
    cfg.RegisterServicesFromAssembly(typeof(Modules.One.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Messaging.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Community.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Payments.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.CRM.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Ops.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Billing.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Lhdn.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Commerce.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Vault.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Communications.Infrastructure.DependencyInjection).Assembly);
});

builder.Services.AddOneModule(builder.Configuration);
builder.Services.AddMessagingModule(builder.Configuration);
builder.Services.AddCommunityModule(builder.Configuration);
builder.Services.AddCrmModule(builder.Configuration);
builder.Services.AddPaymentsModule(builder.Configuration);
builder.Services.AddOpsModule(builder.Configuration);
builder.Services.AddBillingModule(builder.Configuration);
builder.Services.AddLhdnModule(builder.Configuration);
builder.Services.AddCommerceModule(builder.Configuration);
builder.Services.AddVaultModule(builder.Configuration);
builder.Services.AddCommunicationsModule(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseMiddleware<TenantSecurityMiddleware>();
app.UseAuthorization();

app.UseOneSubscriptions();
app.UseMessagingSubscriptions();
app.UseCommunitySubscriptions();
app.UseCrmSubscriptions();
app.UsePaymentsSubscriptions();
app.UseOpsSubscriptions();
app.UseBillingSubscriptions();
app.UseLhdnSubscriptions();
app.UseCommerceSubscriptions();
app.UseVaultSubscriptions();
app.UseCommunicationsSubscriptions();

var eventBus = app.Services.GetRequiredService<IEventBusSubscriptions>();
eventBus.Subscribe<Modules.Lhdn.Contracts.Events.ApiKeyRevokedIntegrationEvent, Lazuar.Api.EventHandlers.ApiKeyRevokedIntegrationEventHandler>();
eventBus.Subscribe<Modules.One.Contracts.WorkspaceUpdatedIntegrationEvent, Lazuar.Api.EventHandlers.WorkspaceUpdatedIntegrationEventHandler>();

var apiGroup = app.MapGroup("/api/v1").RequireCors();

apiGroup.MapOneEndpoints();
apiGroup.MapMessagingEndpoints();
apiGroup.MapCommunityEndpoints();
apiGroup.MapPaymentsEndpoints();
apiGroup.MapOpsEndpoints();
apiGroup.MapBillingEndpoints();
apiGroup.MapLhdnEndpoints();
apiGroup.MapCommerceEndpoints();
apiGroup.MapVaultEndpoints();
apiGroup.MapCommunicationsEndpoints();

var platformGroup = app.MapGroup("/api/v1/platform")
   .RequireCors()
   .RequireAuthorization(policy => policy.RequireRole("SUPER_ADMIN"));

platformGroup.MapPlatformEndpoints();

app.Run();

public partial class Program { }
