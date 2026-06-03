using Serilog;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Tenant.Infrastructure;
using Modules.Messaging.Infrastructure;
using Lazuar.Api;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IExecutionContextAccessor, ExecutionContextAccessor>();
builder.Services.AddSingleton<DatabaseJobTrigger>();
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IMessagingService, ConsoleMessagingService>();
builder.Services.AddSingleton<IEmailService, ConsoleEmailService>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Tenant.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Messaging.Application.DependencyInjection).Assembly);
});

builder.Services.AddTenantModule(builder.Configuration);
builder.Services.AddMessagingModule(builder.Configuration);

var app = builder.Build();

// Decouple Database Migrations from API Pipeline. We Delete the database
// migration invocation scope and Introduce Automated DB Migration CLI Task

app.UseExceptionHandler();
app.UseMessagingSubscriptions();

var apiGroup = app.MapGroup("/api/v1");
apiGroup.MapTenantEndpoints();
apiGroup.MapMessagingEndpoints();

app.Run();

public partial class Program { }
