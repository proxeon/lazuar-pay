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
using Modules.Lhdn.Domain;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

/// <summary>
/// B.9: API key auth — valid / invalid / revoked paths and scope policy vs OrgAdmin.
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
            Scopes = ApiKeyScopes.DefaultDocumentScopes
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
        Assert.That(context.User.HasClaim("scope", ApiKeyScopes.LhdnDocumentsWrite), Is.True);
        Assert.That(context.User.HasClaim("scope", ApiKeyScopes.LhdnDocumentsRead), Is.True);
        Assert.That(context.Items["TenantId"], Is.EqualTo(orgId));
        Assert.That(context.Items["CredentialId"], Is.EqualTo(credentialId));
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
            scopes: [ApiKeyScopes.LhdnDocumentsWrite, ApiKeyScopes.LhdnDocumentsRead]);

        var result = await auth.AuthorizeAsync(apiClient, resource: null, policyName: "OrgAdmin");

        Assert.That(result.Succeeded, Is.False,
            "Stolen/mis-scoped keys must not pass OrgAdmin (key mint, payment config, certs).");
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
        var apiClient = Principal(role: "API_CLIENT", scopes: [ApiKeyScopes.LhdnDocumentsWrite]);

        var result = await auth.AuthorizeAsync(apiClient, null, "IntegrationLhdnDocumentsWrite");

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public async Task IntegrationWrite_Policy_Denies_ApiClient_With_Only_Read_Scope()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(role: "API_CLIENT", scopes: [ApiKeyScopes.LhdnDocumentsRead]);

        var result = await auth.AuthorizeAsync(apiClient, null, "IntegrationLhdnDocumentsWrite");

        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task IntegrationRead_Policy_Allows_Write_Scope_As_Read()
    {
        var auth = BuildAuthorizationService();
        var apiClient = Principal(role: "API_CLIENT", scopes: [ApiKeyScopes.LhdnDocumentsWrite]);

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
                        && ctx.User.HasClaim("scope", ApiKeyScopes.LhdnDocumentsWrite)));
            });

            options.AddPolicy("IntegrationLhdnDocumentsRead", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.IsInRole("SUPER_ADMIN")
                    || ctx.User.IsInRole("ADMIN")
                    || (ctx.User.IsInRole("API_CLIENT")
                        && (ctx.User.HasClaim("scope", ApiKeyScopes.LhdnDocumentsRead)
                            || ctx.User.HasClaim("scope", ApiKeyScopes.LhdnDocumentsWrite))));
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
