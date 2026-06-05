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
using Modules.Tenant.Infrastructure;
using Modules.Messaging.Infrastructure;
using Modules.Community.Infrastructure;
using Modules.CRM.Infrastructure;
using Modules.Payments.Infrastructure;
using Lazuar.Api;
using Lazuar.ApiTypes;

// Resolve the ambiguous reference between Lazuar.ApiTypes and Microsoft.AspNetCore.Mvc
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
    
    // --> ADDED: Instruct the middleware to read the token from the cookie
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
                  .AllowCredentials(); // REQUIRED for cookies to work across ports!
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
    cfg.RegisterServicesFromAssembly(typeof(Modules.Tenant.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Messaging.Application.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Community.Application.DependencyInjection).Assembly); 
    cfg.RegisterServicesFromAssembly(typeof(Modules.Payments.Application.DependencyInjection).Assembly);

    cfg.RegisterServicesFromAssembly(typeof(Modules.Tenant.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Messaging.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Community.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.Payments.Infrastructure.DependencyInjection).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(Modules.CRM.Infrastructure.DependencyInjection).Assembly);
});

// Register Module Services
builder.Services.AddTenantModule(builder.Configuration);
builder.Services.AddMessagingModule(builder.Configuration);
builder.Services.AddCommunityModule(builder.Configuration);
builder.Services.AddCrmModule(builder.Configuration);
builder.Services.AddPaymentsModule(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMessagingSubscriptions();
app.UseCommunitySubscriptions();

var apiGroup = app.MapGroup("/api/v1").RequireCors();

// --- Auth Handlers for Local Dev/Admin Panel ---
apiGroup.MapPost("/platform/auth/login", Results<Ok<LoginResponse>, BadRequest<ProblemDetails>> (
    [FromBody] LoginRequest req,
    IConfiguration config,
    IJwtService jwtService,
    HttpContext ctx) =>
{
    var email = req.Email?.Trim().ToLowerInvariant();
    
    if (string.IsNullOrEmpty(email))
    {
        return TypedResults.BadRequest(new ProblemDetails { Status = 400, Detail = "Email is required." });
    }

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
            new Claim(ClaimTypes.Email, email),
            new Claim("org_id", "7d97963c-063c-4598-86cc-9ddd9d47d9b1"),
            new Claim(ClaimTypes.Role, "SUPER_ADMIN")
        };

        var token = jwtService.GenerateToken(claims, secret, issuer, audience, expiryHours);

        // --> ADDED: Issue the HttpOnly Cookie
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = !app.Environment.IsDevelopment(), // Secure true in Prod, false in Local Dev
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddHours(expiryHours)
        };
        ctx.Response.Cookies.Append("lazuar_auth", token, cookieOptions);

        return TypedResults.Ok(new LoginResponse
        {
            User = new AuthUser { Email = email, Name = "Administrator", Role = "SUPER_ADMIN" }
        });
    }

    return TypedResults.BadRequest(new ProblemDetails { Status = 401, Detail = "Invalid email or password." });
});

apiGroup.MapPost("/platform/auth/logout", (HttpContext ctx) => 
{
    ctx.Response.Cookies.Delete("lazuar_auth");
    return TypedResults.Ok(new StatusResponse { Status = "logged_out" });
});

apiGroup.MapGet("/platform/auth/me", Results<Ok<AuthUser>, UnauthorizedHttpResult> (ClaimsPrincipal principal) =>
{
    var email = principal.FindFirst(ClaimTypes.Email)?.Value;
    if (string.IsNullOrEmpty(email)) return TypedResults.Unauthorized();

    return TypedResults.Ok(new AuthUser { Email = email, Name = "Administrator", Role = "SUPER_ADMIN" });
}).RequireAuthorization();

// Map Minimal API Endpoints
apiGroup.MapTenantEndpoints();
apiGroup.MapMessagingEndpoints();
apiGroup.MapCommunityEndpoints();
apiGroup.MapPaymentsEndpoints();

app.Run();

public partial class Program { }
