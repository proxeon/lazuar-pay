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
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.Configuration;
using BuildingBlocks.Infrastructure.Llm;
using Modules.One.Infrastructure;
using Modules.Messaging.Infrastructure;
using Modules.Community.Infrastructure;
using Modules.CRM.Infrastructure;
using Modules.Payments.Infrastructure;
using Modules.Ops.Infrastructure;
using Lazuar.Api;
using Lazuar.Api.Middleware;
using Lazuar.ApiTypes;

using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddOptions<ResendOptions>()
    .BindConfiguration(ResendOptions.SectionName);

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

builder.Services.AddThinLlmFactory();

builder.Services.AddSingleton<InMemoryEventBus>();
builder.Services.AddSingleton<IEventBusSubscriptions>(sp => sp.GetRequiredService<InMemoryEventBus>());

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
            if (context.Request.Cookies.TryGetValue("lazuar_auth", out var token))
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
        policy.RequireRole("SUPER_ADMIN", "ADMIN");
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

    cfg.RegisterServicesFromAssembly(typeof(Modules.One.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Messaging.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Community.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Payments.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.CRM.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Ops.Infrastructure.DependencyInjection).Assembly);
});

builder.Services.AddOneModule(builder.Configuration);
builder.Services.AddMessagingModule(builder.Configuration);
builder.Services.AddCommunityModule(builder.Configuration);
builder.Services.AddCrmModule(builder.Configuration);
builder.Services.AddPaymentsModule(builder.Configuration);
builder.Services.AddOpsModule(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<TenantSecurityMiddleware>();
app.UseAuthorization();

app.UseOneSubscriptions();
app.UseMessagingSubscriptions();
app.UseCommunitySubscriptions();
app.UseCrmSubscriptions();
app.UsePaymentsSubscriptions();
app.UseOpsSubscriptions();

var apiGroup = app.MapGroup("/api/v1").RequireCors();

apiGroup.MapOneEndpoints();
apiGroup.MapMessagingEndpoints();
apiGroup.MapCommunityEndpoints();
apiGroup.MapPaymentsEndpoints();
apiGroup.MapOpsEndpoints();

app.Run();

public partial class Program { }
