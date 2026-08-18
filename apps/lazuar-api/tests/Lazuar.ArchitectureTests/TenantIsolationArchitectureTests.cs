using System;
using System.IO;
using System.Linq;
using BuildingBlocks.Infrastructure;
using Lazuar.Api.Middleware;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace Lazuar.ArchitectureTests;

/// <summary>
/// C.2 — Guardrails for fail-closed tenant isolation (query filters, middleware allowlist).
/// </summary>
[TestFixture]
public class TenantIsolationArchitectureTests
{
    private static string FindRepoFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            // Architecture tests bin sits under apps/lazuar-api/tests/...
            candidate = Path.Combine(
                new[] { dir.FullName, "apps", "lazuar-api" }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        Assert.Fail($"Could not locate source file: {string.Join("/", relativeParts)}");
        return null!;
    }

    [Test]
    public void PlatformDbContext_Filter_Must_Not_Treat_Empty_Tenant_As_All_Rows()
    {
        var path = FindRepoFile(
            "BuildingBlocks", "Infrastructure", "PlatformDbContext.cs");
        var source = File.ReadAllText(path);

        // Fail-open pattern: TenantId == Empty || org match
        Assert.That(
            source.Contains("TenantId == Guid.Empty ||", StringComparison.Ordinal)
            || source.Contains("TenantId == Guid.Empty||", StringComparison.Ordinal),
            Is.False,
            "PlatformDbContext must not use fail-open Empty-tenant-as-all-rows filter.");

        Assert.That(
            source.Contains("e.OrganizationId == ExecutionContext.TenantId", StringComparison.Ordinal)
            || source.Contains("OrganizationId == ExecutionContext.TenantId", StringComparison.Ordinal),
            Is.True,
            "PlatformDbContext global filter must require OrganizationId == ambient TenantId (fail-closed).");
    }

    [Test]
    public void OpsDbContext_HasQueryFilter_Override_Must_Include_OrganizationId()
    {
        var path = FindRepoFile(
            "Modules", "Ops", "Infrastructure", "OpsDbContext.cs");
        var source = File.ReadAllText(path);

        // Soft-delete override must still include tenant predicate (fail-closed).
        Assert.That(source, Does.Contain("HasQueryFilter"));
        Assert.That(
            source.Contains("OrganizationId == ExecutionContext.TenantId", StringComparison.Ordinal),
            Is.True,
            "OpsConversation HasQueryFilter override must include OrganizationId tenant match.");
        Assert.That(
            source.Contains("TenantId == Guid.Empty ||", StringComparison.Ordinal),
            Is.False,
            "OpsConversation filter must not fail-open on empty TenantId.");
    }

    [Test]
    public void TenantSecurityMiddleware_Requires_Tenant_For_OrgAdmin_Modules()
    {
        Assert.That(TenantSecurityMiddleware.RequiresTenantContext(new PathString("/api/v1/admin/commerce")), Is.True);
        Assert.That(TenantSecurityMiddleware.RequiresTenantContext(new PathString("/api/v1/lhdn/documents")), Is.True);
        Assert.That(TenantSecurityMiddleware.RequiresTenantContext(new PathString("/api/v1/ops/stream")), Is.True);
        Assert.That(TenantSecurityMiddleware.RequiresTenantContext(new PathString("/api/v1/messaging/notify")), Is.True);
        Assert.That(TenantSecurityMiddleware.RequiresTenantContext(new PathString("/api/v1/one/storage/presigned-url")), Is.True);
        Assert.That(TenantSecurityMiddleware.RequiresTenantContext(new PathString("/api/v1/one/api-keys")), Is.True);
    }

    [Test]
    public void TenantSecurityMiddleware_Exempts_Public_Auth_Webhooks_And_Workspace_Surfaces()
    {
        Assert.That(TenantSecurityMiddleware.IsTenantExemptPath(new PathString("/health")), Is.True);
        Assert.That(TenantSecurityMiddleware.IsTenantExemptPath(new PathString("/api/v1/public/commerce/checkout")), Is.True);
        Assert.That(TenantSecurityMiddleware.IsTenantExemptPath(new PathString("/api/v1/public/one/acme/branding")), Is.True);
        Assert.That(TenantSecurityMiddleware.IsTenantExemptPath(new PathString("/api/v1/webhooks/payments/stripe")), Is.True);
        Assert.That(TenantSecurityMiddleware.IsTenantExemptPath(new PathString("/api/v1/one/auth/login")), Is.True);
        Assert.That(TenantSecurityMiddleware.IsTenantExemptPath(new PathString("/api/v1/one/public/register")), Is.True);
        Assert.That(TenantSecurityMiddleware.IsTenantExemptPath(new PathString("/api/v1/one/public/pricing")), Is.True);
        Assert.That(TenantSecurityMiddleware.IsTenantExemptPath(new PathString("/api/v1/one/workspaces")), Is.True);
        Assert.That(TenantSecurityMiddleware.IsTenantExemptPath(new PathString("/api/v1/one/me/entitlements")), Is.True);
        Assert.That(TenantSecurityMiddleware.IsTenantExemptPath(new PathString("/api/v1/one/integrations/workspaces/provision")), Is.True);

        // Tenant-scoped One routes are not exempt from require-tenant when not listed above.
        Assert.That(TenantSecurityMiddleware.IsTenantExemptPath(new PathString("/api/v1/one/storage/presigned-url")), Is.False);
        Assert.That(TenantSecurityMiddleware.IsTenantExemptPath(new PathString("/api/v1/one/api-keys")), Is.False);
    }

    [Test]
    public void Boot_Migrate_Does_Not_Swallow_PendingModelChanges()
    {
        var path = FindRepoFile("src", "Lazuar.Api", "Composition", "DatabaseMigrationExtensions.cs");
        var source = File.ReadAllText(path);
        Assert.That(source, Does.Not.Contain("PendingModelChanges"));
        Assert.That(source, Does.Contain("throw;"));
    }

    [Test]
    public void CommerceRepository_IgnoreQueryFilters_Id_Lookups_Require_OrganizationId()
    {
        var path = FindRepoFile(
            "Modules", "Commerce", "Infrastructure", "Repositories", "CommerceRepository.cs");
        var source = File.ReadAllText(path);

        Assert.That(source, Does.Contain("p.OrganizationId == organizationId && p.Id == id"));
        Assert.That(source, Does.Contain("s.OrganizationId == organizationId && s.Id == id"));
        Assert.That(source, Does.Contain("t.OrganizationId == organizationId && t.Id == id"));
        Assert.That(source, Does.Contain("GetSubscriptionByIdForPortalTokenAsync"));
    }

    [Test]
    public void DocumentLinkSigner_Draft_And_Final_Payloads_Differ()
    {
        var exp = 1_700_000_000L;
        var tenant = "acme";
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var draft = DocumentLinkSigner.DraftDocumentPayload(tenant, id, exp);
        var finalDoc = DocumentLinkSigner.FinalDocumentPayload(tenant, id, exp);

        Assert.That(draft, Is.EqualTo($"acme:draft:{id}:{exp}"));
        Assert.That(finalDoc, Is.EqualTo($"acme:{id}:{exp}"));
        Assert.That(draft, Is.Not.EqualTo(finalDoc));
    }
}
