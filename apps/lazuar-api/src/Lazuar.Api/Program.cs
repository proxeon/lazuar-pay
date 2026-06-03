using Serilog;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Tenant.Infrastructure;
using Modules.Messaging.Infrastructure;
using Lazuar.Api;
// Add these two usings for the database creator
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Register Shared Infrastructure & Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IExecutionContextAccessor, ExecutionContextAccessor>();
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IJwtService, JwtService>();

// satisfies cross-module notification dependencies
builder.Services.AddSingleton<IMessagingService, ConsoleMessagingService>();
builder.Services.AddSingleton<IEmailService, ConsoleEmailService>();

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

// ==========================================
// Auto-create Database Tables on Startup (Multi-DbContext Support)
// ==========================================
using (var scope = app.Services.CreateScope())
{
    // 1. EnsureCreated() works for the first context. It creates the physical DB and Tenant tables.
    var tenantDb = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
    tenantDb.Database.EnsureCreated();

    // 2. For subsequent contexts, we must force table creation.
    var messagingDb = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
    var messagingCreator = messagingDb.GetService<IRelationalDatabaseCreator>();
    
    try
    {
        messagingCreator.CreateTables();
    }
    catch 
    {
        // Suppress error on subsequent hot-reloads where the tables already exist.
        // Postgres will throw "42P07: relation already exists" which is safe to ignore here.
    }
}
// ==========================================

app.UseExceptionHandler();

// Register cross-module event subscriptions
app.UseMessagingSubscriptions();

// Map Module endpoints under /api/v1
var apiGroup = app.MapGroup("/api/v1");
apiGroup.MapTenantEndpoints();
apiGroup.MapMessagingEndpoints();

app.Run();

public partial class Program { }
