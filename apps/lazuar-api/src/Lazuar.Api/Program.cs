using Serilog;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.Configuration;
using Modules.Tenant.Infrastructure;
using Modules.Messaging.Infrastructure;
using Modules.Community.Infrastructure;
using Modules.CRM.Infrastructure;
using Modules.Payments.Infrastructure;
using Lazuar.Api;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// --- Configure Options ---
builder.Services.AddOptions<ResendOptions>()
    .BindConfiguration(ResendOptions.SectionName);

// --- Configure HttpClients ---
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
builder.Services.AddScoped<IExecutionContextAccessor, ExecutionContextAccessor>();
builder.Services.AddSingleton<DatabaseJobTrigger>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IMessagingService, ConsoleMessagingService>();
builder.Services.AddSingleton<IEmailService, ResendEmailService>();

// Configure the singleton in-memory event bus and its subscription contract
builder.Services.AddSingleton<InMemoryEventBus>();
builder.Services.AddSingleton<IEventBusSubscriptions>(sp => sp.GetRequiredService<InMemoryEventBus>());

// --- Configure JWT Authentication ---
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
});

// --- Configure Authorization Policy for Modules ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OrgAdmin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("SUPER_ADMIN", "ADMIN");
    });
});

// --- Configure CORS Services ---
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

// Configure JSON payload naming policies exclusively
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddMediatR(cfg =>
{
    // Register Application Assemblies
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Tenant.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Messaging.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Community.Application.DependencyInjection).Assembly); 
    cfg.RegisterServicesFromAssembly(typeof(Modules.Payments.Application.DependencyInjection).Assembly);

    // CRUCIAL MONOLITH FIX: Register Infrastructure Assemblies so MediatR discovers newly moved handlers
    cfg.RegisterServicesFromAssembly(typeof(Modules.Tenant.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Messaging.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Community.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Payments.Infrastructure.DependencyInjection).Assembly);
});

// Register Module Services
builder.Services.AddTenantModule(builder.Configuration);
builder.Services.AddMessagingModule(builder.Configuration);
builder.Services.AddCommunityModule(builder.Configuration);
builder.Services.AddCrmModule(builder.Configuration);
builder.Services.AddPaymentsModule(builder.Configuration);

var app = builder.Build();

// Decouple Database Migrations from API Pipeline. We Delete the database
// migration invocation scope and Introduce Automated DB Migration CLI Task

app.UseExceptionHandler();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// Register Cross-Module Event Subscriptions
app.UseMessagingSubscriptions();
app.UseCommunitySubscriptions();

var apiGroup = app.MapGroup("/api/v1").RequireCors();

// --- Fallback Auth Handlers for Local Dev/Admin Panel ---
apiGroup.MapPost("/platform/auth/login", (
    [FromBody] LoginRequest req,
    IConfiguration config,
    IJwtService jwtService) =>
{
    var email = req.Email?.Trim().ToLowerInvariant();
    
    if (string.IsNullOrEmpty(email))
    {
        return Results.Json(new { error = "Email address is required." }, statusCode: 400);
    }

    // Accept baseline passwords as mapped in configuration documentation
    if ((email == "admin@lazuars.io" || email == "sysadmin@lazuars.io" || email == "admin@yourdomain.com") 
        && req.Password == "Password123!")
    {
        var secret = config["Jwt:Secret"] ?? "secure_development_key_minimum_32_characters_long";
        var issuer = config["Jwt:Issuer"] ?? "lazuar-api";
        var audience = config["Jwt:Audience"] ?? "lazuar-clients";
        var expiryHours = config.GetValue<int>("Jwt:ExpiryHours", 24);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "018f3a3f-3610-73bf-baef-c07a3c3df9ee"),
            new Claim(ClaimTypes.Email, email), // References local non-null validated string
            new Claim("org_id", "7d97963c-063c-4598-86cc-9ddd9d47d9b1"), // Base Tenant ID
            new Claim(ClaimTypes.Role, "SUPER_ADMIN")
        };

        var token = jwtService.GenerateToken(claims, secret, issuer, audience, expiryHours);

        return Results.Ok(new
        {
            token,
            user = new
            {
                email = email,
                name = "Administrator",
                role = "SUPER_ADMIN"
            }
        });
    }

    return Results.Json(new { error = "Invalid email or password." }, statusCode: 401);
});

apiGroup.MapGet("/platform/auth/me", (ClaimsPrincipal principal) =>
{
    var email = principal.FindFirst(ClaimTypes.Email)?.Value;
    if (string.IsNullOrEmpty(email)) return Results.Unauthorized();

    return Results.Ok(new
    {
        email,
        name = "Administrator",
        role = "SUPER_ADMIN"
    });
}).RequireAuthorization();

// Map Minimal API Endpoints
apiGroup.MapTenantEndpoints();
apiGroup.MapMessagingEndpoints();
apiGroup.MapCommunityEndpoints();
apiGroup.MapPaymentsEndpoints();

app.Run();

public record LoginRequest(string Email, string Password);

public partial class Program { }
