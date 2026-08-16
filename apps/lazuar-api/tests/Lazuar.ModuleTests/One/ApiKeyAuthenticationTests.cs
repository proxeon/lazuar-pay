using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Modules.One.Domain;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

/// <summary>
/// B.9 / Phase 1: API key auth — valid / invalid / revoked paths and scope policy vs OrgAdmin.
/// </summary>
[TestFixture]
public class ApiKeyAuthenticationTests
{
    [Test]
    public async Task Valid_Cached_Key_Sets_ApiClient_Claims_And_Scopes()
    {
        var orgId = Guid.CreateVersion7();
        var credentialId = Guid.CreateVersion7();
        const string plainKey = "sk_test_validkeyabcdefghijklmnop";
        const string keyHash = "hash:valid";

        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.HashToken(plainKey).Returns(keyHash);

        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set($"ApiKey_{keyHash}", new ApiKeyAuthenticationMiddleware.ApiKeyCacheEntry
        {
            CredentialId = credentialId,
            OrganizationId = orgId,
            Scopes = PlatformApiScopes.DefaultDocumentScopes
        });

        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        context.Request.Headers.Authorization = $"Bearer {plainKey}";

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ApiKeyAuthenticationMiddleware(next, cache, tokens);
        await middleware.InvokeAsync(context);

        Assert.That(nextCalled, Is.True);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(context.User.Identity?.IsAuthenticated, Is.True);
        Assert.That(context.User.IsInRole("API_CLIENT"), Is.True);
        Assert.That(context.User.IsInRole("ADMIN"), Is.False);
        Assert.That(context.User.FindFirstValue("CredentialId"), Is.EqualTo(credentialId.ToString()));
        Assert.That(context.User.FindFirstValue("TenantId"), Is.EqualTo(orgId.ToString()));
        Assert.That(context.User.FindFirstValue("IsTestMode"), Is.EqualTo("true"));
        Assert.That(context.User.HasClaim("scope", PlatformApiScopes.LhdnDocumentsWrite), Is.True);
        Assert.That(context.User.HasClaim("scope", PlatformApiScopes.LhdnDocumentsRead), Is.True);
        Assert.That(context.Items["TenantId"], Is.EqualTo(orgId));
        Assert.That(context.Items["CredentialId"], Is.EqualTo(credentialId));
    }

    [Test]
    public async Task Valid_Cached_Key_With_Payment_Scopes_Sets_Scope_Claims()
    {
        // Checklist 1.6.1 stand-in until Phase 2 checkout routes exist: auth materializes scope claims.
        var orgId = Guid.CreateVersion7();
        var credentialId = Guid.CreateVersion7();
        const string plainKey = "sk_test_paymentonlyabcdefghijklmn";
        const string keyHash = "hash:pay";

        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.HashToken(plainKey).Returns(keyHash);

        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set($"ApiKey_{keyHash}", new ApiKeyAuthenticationMiddleware.ApiKeyCacheEntry
        {
            CredentialId = credentialId,
            OrganizationId = orgId,
            Scopes = PlatformApiScopes.DefaultAuraIntegratorScopes
        });

        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        context.Request.Headers.Authorization = $"Bearer {plainKey}";

        var middleware = new ApiKeyAuthenticationMiddleware(
            _ => Task.CompletedTask,
            cache,
            tokens);
        await middleware.InvokeAsync(context);

        Assert.That(context.User.Identity?.IsAuthenticated, Is.True);
        Assert.That(context.User.HasClaim("scope", PlatformApiScopes.PaymentsCheckoutsWrite), Is.True);
        Assert.That(context.User.HasClaim("scope", PlatformApiScopes.PaymentsCheckoutsRead), Is.True);
        Assert.That(context.User.HasClaim("scope", PlatformApiScopes.WebhooksEndpointsManage), Is.True);
        Assert.That(context.User.HasClaim("scope", PlatformApiScopes.LhdnDocumentsWrite), Is.False);
        Assert.That(context.User.FindFirstValue("TenantId"), Is.EqualTo(orgId.ToString()));
        Assert.That(context.Items["TenantId"], Is.EqualTo(orgId));
    }

    [Test]
    public async Task Webhooks_Manage_Policy_Allows_ApiClient_With_Scope()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(
            role: "API_CLIENT",
            scopes: [PlatformApiScopes.WebhooksEndpointsManage]);

        var result = await auth.AuthorizeAsync(apiClient, null, "IntegrationWebhooksEndpointsManage");
        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public async Task Webhooks_Manage_Policy_Denies_ApiClient_Without_Scope()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(
            role: "API_CLIENT",
            scopes: [PlatformApiScopes.PaymentsCheckoutsWrite]);

        var result = await auth.AuthorizeAsync(apiClient, null, "IntegrationWebhooksEndpointsManage");
        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task Invalid_Or_Unknown_Key_Returns_401_And_Does_Not_Call_Next()
    {
        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.HashToken(Arg.Any<string>()).Returns("hash:unknown");

        // No keyed SQL factories → LookupCredentialAsync returns null (invalid / not found / revoked).
        var services = new ServiceCollection().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Headers.Authorization = "Bearer sk_live_doesnotexist";

        var nextCalled = false;
        var middleware = new ApiKeyAuthenticationMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new MemoryCache(new MemoryCacheOptions()),
            tokens);

        await middleware.InvokeAsync(context);

        Assert.That(nextCalled, Is.False);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
        Assert.That(context.User.Identity?.IsAuthenticated ?? false, Is.False);
    }

    [Test]
    public async Task One_Only_Lookup_Lhdn_Only_Key_Returns_401_And_Does_Not_Call_Lhdn_Factory()
    {
        // R05 regression: dual-read removed — keys present only on lhdn.DeveloperApiKeys fail closed.
        // LhdnSqlConnectionFactory must never be consulted even when registered.
        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.HashToken(Arg.Any<string>()).Returns("hash:legacy-only");

        var lhdnFactory = Substitute.For<ISqlConnectionFactory>();
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ISqlConnectionFactory>("LhdnSqlConnectionFactory", lhdnFactory);
        // Intentionally no OneSqlConnectionFactory → One miss; legacy branch no longer exists.
        var sp = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = sp };
        context.Request.Headers.Authorization = "Bearer sk_live_legacyonlykey";

        var nextCalled = false;
        var middleware = new ApiKeyAuthenticationMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new MemoryCache(new MemoryCacheOptions()),
            tokens);

        await middleware.InvokeAsync(context);

        Assert.That(nextCalled, Is.False);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
        Assert.That(context.User.Identity?.IsAuthenticated ?? false, Is.False);
        lhdnFactory.DidNotReceive().CreateConnection();
    }

    [Test]
    public async Task Revoked_Key_After_Cache_Eviction_Returns_401()
    {
        // Mirrors production: revoke removes ApiKey_{hash} from cache; next request re-looks up
        // IsActive=true row and fails closed when missing (revoked keys are inactive).
        var tokens = Substitute.For<ITokenGeneratorService>();
        const string plainKey = "sk_live_revokedkeyxyz";
        const string keyHash = "hash:revoked";
        tokens.HashToken(plainKey).Returns(keyHash);

        var cache = new MemoryCache(new MemoryCacheOptions());
        // Simulate post-revoke eviction (ApiKeyRevokedIntegrationEventHandler).
        Assert.That(cache.TryGetValue($"ApiKey_{keyHash}", out _), Is.False);

        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        context.Request.Headers.Authorization = plainKey; // raw sk_ form also accepted

        var nextCalled = false;
        var middleware = new ApiKeyAuthenticationMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            cache,
            tokens);

        await middleware.InvokeAsync(context);

        Assert.That(nextCalled, Is.False);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public async Task OrgAdmin_Policy_Denies_ApiClient_Even_With_Full_Document_Scopes()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(
            role: "API_CLIENT",
            scopes: [PlatformApiScopes.LhdnDocumentsWrite, PlatformApiScopes.LhdnDocumentsRead]);

        var result = await auth.AuthorizeAsync(apiClient, resource: null, policyName: "OrgAdmin");

        Assert.That(result.Succeeded, Is.False,
            "Stolen/mis-scoped keys must not pass OrgAdmin (key mint, payment config, certs).");
    }

    [Test]
    public async Task OrgAdmin_Policy_Denies_ApiClient_With_Payment_Scopes()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(
            role: "API_CLIENT",
            scopes:
            [
                PlatformApiScopes.PaymentsCheckoutsWrite,
                PlatformApiScopes.PaymentsCheckoutsRead
            ]);

        var result = await auth.AuthorizeAsync(apiClient, null, "OrgAdmin");

        Assert.That(result.Succeeded, Is.False,
            "Payment-scoped machine keys must not mint keys or write payment-config.");
    }

    [Test]
    public async Task OrgAdmin_Policy_Allows_Human_Admin()
    {
        var auth = BuildAuthorizationService();
        var admin = Principal(role: "ADMIN");

        var result = await auth.AuthorizeAsync(admin, resource: null, policyName: "OrgAdmin");

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public async Task IntegrationWrite_Policy_Allows_ApiClient_With_Write_Scope()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(role: "API_CLIENT", scopes: [PlatformApiScopes.LhdnDocumentsWrite]);

        var result = await auth.AuthorizeAsync(apiClient, null, "IntegrationLhdnDocumentsWrite");

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public async Task IntegrationWrite_Policy_Denies_ApiClient_With_Only_Read_Scope()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(role: "API_CLIENT", scopes: [PlatformApiScopes.LhdnDocumentsRead]);

        var result = await auth.AuthorizeAsync(apiClient, null, "IntegrationLhdnDocumentsWrite");

        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task IntegrationRead_Policy_Allows_Write_Scope_As_Read()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(role: "API_CLIENT", scopes: [PlatformApiScopes.LhdnDocumentsWrite]);

        var result = await auth.AuthorizeAsync(apiClient, null, "IntegrationLhdnDocumentsRead");

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public async Task IntegrationRead_Policy_Denies_ApiClient_Without_Scopes()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(role: "API_CLIENT", scopes: []);

        var result = await auth.AuthorizeAsync(apiClient, null, "IntegrationLhdnDocumentsRead");

        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task Payments_Write_Policy_Allows_ApiClient_With_Write_Scope()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(
            role: "API_CLIENT",
            scopes: [PlatformApiScopes.PaymentsCheckoutsWrite]);

        var result = await auth.AuthorizeAsync(apiClient, null, "IntegrationPaymentsCheckoutsWrite");

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public async Task Payments_Write_Policy_Denies_Read_Only_Payment_Scope()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(
            role: "API_CLIENT",
            scopes: [PlatformApiScopes.PaymentsCheckoutsRead]);

        var result = await auth.AuthorizeAsync(apiClient, null, "IntegrationPaymentsCheckoutsWrite");

        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task Payments_Read_Policy_Allows_Write_Scope_As_Read()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(
            role: "API_CLIENT",
            scopes: [PlatformApiScopes.PaymentsCheckoutsWrite]);

        var result = await auth.AuthorizeAsync(apiClient, null, "IntegrationPaymentsCheckoutsRead");

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public async Task Payment_Only_Key_Denied_On_Lhdn_Write_Policy()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(
            role: "API_CLIENT",
            scopes:
            [
                PlatformApiScopes.PaymentsCheckoutsWrite,
                PlatformApiScopes.PaymentsCheckoutsRead
            ]);

        var lhdnWrite = await auth.AuthorizeAsync(apiClient, null, "IntegrationLhdnDocumentsWrite");
        var lhdnRead = await auth.AuthorizeAsync(apiClient, null, "IntegrationLhdnDocumentsRead");
        var paymentsWrite = await auth.AuthorizeAsync(apiClient, null, "IntegrationPaymentsCheckoutsWrite");

        Assert.That(lhdnWrite.Succeeded, Is.False, "Cross-product isolation: payments ≠ LHDN write");
        Assert.That(lhdnRead.Succeeded, Is.False, "Cross-product isolation: payments ≠ LHDN read");
        Assert.That(paymentsWrite.Succeeded, Is.True);
    }

    [Test]
    public async Task Lhdn_Only_Key_Denied_On_Payments_Write_Policy()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(
            role: "API_CLIENT",
            scopes:
            [
                PlatformApiScopes.LhdnDocumentsWrite,
                PlatformApiScopes.LhdnDocumentsRead
            ]);

        var paymentsWrite = await auth.AuthorizeAsync(apiClient, null, "IntegrationPaymentsCheckoutsWrite");
        var paymentsRead = await auth.AuthorizeAsync(apiClient, null, "IntegrationPaymentsCheckoutsRead");

        Assert.That(paymentsWrite.Succeeded, Is.False);
        Assert.That(paymentsRead.Succeeded, Is.False);
    }

    [Test]
    public async Task Payments_Me_Policy_Allows_ApiClient_With_Any_Payments_Scope()
    {
        var auth = BuildAuthorizationService();
        foreach (var scope in new[]
                 {
                     PlatformApiScopes.PaymentsCheckoutsWrite,
                     PlatformApiScopes.PaymentsCheckoutsRead
                 })
        {
            var apiClient = Principal(role: "API_CLIENT", scopes: [scope]);
            var result = await auth.AuthorizeAsync(apiClient, null, "IntegrationPaymentsMe");
            Assert.That(result.Succeeded, Is.True, $"scope {scope} should pass IntegrationPaymentsMe");
        }
    }

    [Test]
    public async Task Payments_Me_Policy_Denies_Lhdn_Only_Key()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(
            role: "API_CLIENT",
            scopes: [PlatformApiScopes.LhdnDocumentsWrite, PlatformApiScopes.LhdnDocumentsRead]);

        var result = await auth.AuthorizeAsync(apiClient, null, "IntegrationPaymentsMe");
        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task Payments_Me_Policy_Denies_Webhooks_Manage_Only_Key()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(
            role: "API_CLIENT",
            scopes: [PlatformApiScopes.WebhooksEndpointsManage]);

        var result = await auth.AuthorizeAsync(apiClient, null, "IntegrationPaymentsMe");
        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task Payments_Me_Policy_Denies_Human_Admin()
    {
        var auth = BuildAuthorizationService();
        var admin = Principal(role: "ADMIN");

        var result = await auth.AuthorizeAsync(admin, null, "IntegrationPaymentsMe");
        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task Commerce_Subscriptions_Write_Implies_Read_And_Denies_Payments_Only()
    {
        var auth = BuildAuthorizationService();
        var write = Principal(role: "API_CLIENT", scopes: [PlatformApiScopes.CommerceSubscriptionsWrite]);
        var read = Principal(role: "API_CLIENT", scopes: [PlatformApiScopes.CommerceSubscriptionsRead]);
        var payments = Principal(role: "API_CLIENT", scopes: [PlatformApiScopes.PaymentsCheckoutsWrite]);

        Assert.That((await auth.AuthorizeAsync(write, null, "IntegrationCommerceSubscriptionsWrite")).Succeeded, Is.True);
        Assert.That((await auth.AuthorizeAsync(write, null, "IntegrationCommerceSubscriptionsRead")).Succeeded, Is.True);
        Assert.That((await auth.AuthorizeAsync(read, null, "IntegrationCommerceSubscriptionsWrite")).Succeeded, Is.False);
        Assert.That((await auth.AuthorizeAsync(read, null, "IntegrationCommerceSubscriptionsRead")).Succeeded, Is.True);
        Assert.That((await auth.AuthorizeAsync(payments, null, "IntegrationCommerceSubscriptionsRead")).Succeeded, Is.False);
    }

    /// <summary>Mirrors host policies in Program.cs (OrgAdmin vs Integration*).</summary>
    private static IAuthorizationService BuildAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("OrgAdmin", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole("SUPER_ADMIN", "ADMIN");
            });

            options.AddPolicy("IntegrationLhdnDocumentsWrite", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole("SUPER_ADMIN")
                    || ctx.User.IsInRole("ADMIN")
                    || (ctx.User.IsInRole("API_CLIENT")
                        && ctx.User.HasClaim("scope", PlatformApiScopes.LhdnDocumentsWrite)));
            });

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

            options.AddPolicy("IntegrationPaymentsCheckoutsWrite", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole("SUPER_ADMIN")
                    || ctx.User.IsInRole("ADMIN")
                    || (ctx.User.IsInRole("API_CLIENT")
                        && ctx.User.HasClaim("scope", PlatformApiScopes.PaymentsCheckoutsWrite)));
            });

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

            options.AddPolicy("IntegrationWebhooksEndpointsManage", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole("SUPER_ADMIN")
                    || ctx.User.IsInRole("ADMIN")
                    || (ctx.User.IsInRole("API_CLIENT")
                        && ctx.User.HasClaim("scope", PlatformApiScopes.WebhooksEndpointsManage)));
            });

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
        services.AddSingleton<IAuthorizationHandler, PassThroughHandler>();

        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal Principal(string role, IReadOnlyList<string>? scopes = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test"),
            new(ClaimTypes.Role, role)
        };

        if (scopes is not null)
        {
            foreach (var scope in scopes)
            {
                claims.Add(new Claim("scope", scope));
            }
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private sealed class PassThroughHandler : IAuthorizationHandler
    {
        public Task HandleAsync(AuthorizationHandlerContext context) => Task.CompletedTask;
    }
}
