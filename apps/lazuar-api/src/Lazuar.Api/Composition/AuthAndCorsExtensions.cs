using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Modules.One.Domain;

namespace Lazuar.Api.Composition;

/// <summary>
/// Host-owned JWT auth, authorization policy catalog, and default CORS.
/// Policy names are shared contracts consumed by module endpoints — do not rename lightly.
/// </summary>
public static class AuthAndCorsExtensions
{
    private const string DefaultDevJwtSecret = "secure_development_key_minimum_32_characters_long";

    public static IServiceCollection AddLazuarAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var jwtSecret = configuration["Jwt:Secret"];
        if (environment.IsProduction())
        {
            if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret == DefaultDevJwtSecret)
            {
                throw new InvalidOperationException(
                    "Jwt:Secret must be configured to a non-default value in Production.");
            }
        }

        var guardedJwtSecret = string.IsNullOrWhiteSpace(jwtSecret) ? DefaultDevJwtSecret : jwtSecret;

        services.AddAuthentication(options =>
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
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "lazuar-api",
                    ValidAudience = configuration["Jwt:Audience"] ?? "lazuar-clients",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(guardedJwtSecret))
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Dual cookie realm: platform admin vs product console.
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

        return services;
    }

    public static IServiceCollection AddLazuarAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
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
                        && ctx.User.HasClaim("scope", PlatformApiScopes.LhdnDocumentsWrite)));
            });

            // LHDN document read (GET status): human admins bypass; API_CLIENT needs read or write (write implies read).
            options.AddPolicy("IntegrationLhdnDocumentsRead", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole("SUPER_ADMIN")
                    || ctx.User.IsInRole("ADMIN")
                    || (ctx.User.IsInRole("API_CLIENT")
                        && (ctx.User.HasClaim("scope", PlatformApiScopes.LhdnDocumentsRead)
                            || ctx.User.HasClaim("scope", PlatformApiScopes.LhdnDocumentsWrite))));
            });

            // Payments checkouts write (M2M ad-hoc checkout create — Phase 2 routes attach this policy).
            options.AddPolicy("IntegrationPaymentsCheckoutsWrite", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole("SUPER_ADMIN")
                    || ctx.User.IsInRole("ADMIN")
                    || (ctx.User.IsInRole("API_CLIENT")
                        && ctx.User.HasClaim("scope", PlatformApiScopes.PaymentsCheckoutsWrite)));
            });

            // Payments checkouts read (poll status): write implies read.
            options.AddPolicy("IntegrationPaymentsCheckoutsRead", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole("SUPER_ADMIN")
                    || ctx.User.IsInRole("ADMIN")
                    || (ctx.User.IsInRole("API_CLIENT")
                        && (ctx.User.HasClaim("scope", PlatformApiScopes.PaymentsCheckoutsRead)
                            || ctx.User.HasClaim("scope", PlatformApiScopes.PaymentsCheckoutsWrite))));
            });

            // Optional: manage outbound webhook endpoints via API (console/OrgAdmin remains primary v1).
            options.AddPolicy("IntegrationWebhooksEndpointsManage", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole("SUPER_ADMIN")
                    || ctx.User.IsInRole("ADMIN")
                    || (ctx.User.IsInRole("API_CLIENT")
                        && ctx.User.HasClaim("scope", PlatformApiScopes.WebhooksEndpointsManage)));
            });

            // K1 introspect only — API_CLIENT + any payments.* scope. Humans must not pass.
            options.AddPolicy("IntegrationPaymentsMe", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole("API_CLIENT")
                    && (ctx.User.HasClaim("scope", PlatformApiScopes.PaymentsCheckoutsWrite)
                        || ctx.User.HasClaim("scope", PlatformApiScopes.PaymentsCheckoutsRead)));
            });

            options.AddPolicy("IntegrationCommerceSubscriptionsWrite", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole("SUPER_ADMIN")
                    || ctx.User.IsInRole("ADMIN")
                    || (ctx.User.IsInRole("API_CLIENT")
                        && ctx.User.HasClaim("scope", PlatformApiScopes.CommerceSubscriptionsWrite)));
            });

            options.AddPolicy("IntegrationCommerceSubscriptionsRead", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole("SUPER_ADMIN")
                    || ctx.User.IsInRole("ADMIN")
                    || (ctx.User.IsInRole("API_CLIENT")
                        && (ctx.User.HasClaim("scope", PlatformApiScopes.CommerceSubscriptionsRead)
                            || ctx.User.HasClaim("scope", PlatformApiScopes.CommerceSubscriptionsWrite))));
            });
        });

        return services;
    }

    public static IServiceCollection AddLazuarCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                var corsOrigins = configuration["App:CorsOrigins"];
                if (!string.IsNullOrEmpty(corsOrigins))
                {
                    var origins = corsOrigins.Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

        return services;
    }
}
