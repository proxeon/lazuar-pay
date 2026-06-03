// apps/lazuar-api/src/Lazuar.Api/Program.cs
using Serilog;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Tenant.Infrastructure;
using Modules.Messaging.Infrastructure;
using Lazuar.Api;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Serilog for structured, production-ready logging
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Lazuar.Api")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Execute strict startup security checks
var jwtSecret = builder.Configuration.GetValue<string>("Jwt:Secret");
if (builder.Environment.IsProduction() || builder.Environment.IsStaging())
{
    if (string.IsNullOrWhiteSpace(jwtSecret) || 
        jwtSecret == "secure_development_key_minimum_32_characters_long")
    {
        Log.Fatal("Security Breach: Production execution blocked. JWT Secret is missing or using insecure default key!");
        throw new ApplicationException("Production execution blocked: Insecure JWT Secret configuration.");
    }
}

// Register Shared Infrastructure & Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IExecutionContextAccessor, ExecutionContextAccessor>();
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IJwtService, JwtService>();

// Add exception handler middleware
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Register MediatR across all modules
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Tenant.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Messaging.Application.DependencyInjection).Assembly);
});

// Register Modules with isolated configuration contexts
builder.Services.AddTenantModule(builder.Configuration);
builder.Services.AddMessagingModule(builder.Configuration);

var app = builder.Build();

// 3. Orchestrate Database Migrations sequentially before starting the HTTP server
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("Starting database migration orchestration...");
    
    try
    {
        // Migrate Tenant Module Schema
        var tenantContext = services.GetRequiredService<TenantDbContext>();
        logger.LogInformation("Applying tenant schema migrations...");
        await tenantContext.Database.MigrateAsync();

        // Migrate Messaging Module Schema
        var messagingContext = services.GetRequiredService<MessagingDbContext>();
        logger.LogInformation("Applying messaging schema migrations...");
        await messagingContext.Database.MigrateAsync();

        logger.LogInformation("Database migration completed successfully.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Critical database migration failure. Application startup aborted!");
        throw;
    }
}

app.UseExceptionHandler();

// Register cross-module event subscriptions
app.UseMessagingSubscriptions();

// Map Module endpoints under /api/v1
var apiGroup = app.MapGroup("/api/v1");
apiGroup.MapTenantEndpoints();
apiGroup.MapMessagingEndpoints();

app.Run();

public partial class Program { }
