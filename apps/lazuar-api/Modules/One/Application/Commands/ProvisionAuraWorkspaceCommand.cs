using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.One.Contracts;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public record ProvisionAuraWorkspaceResult(
    Guid WorkspaceId,
    string Slug,
    /// <summary>Normalized external org id (same as <see cref="ExternalOrgId"/>; kept for Aura clients).</summary>
    string AuraOrgId,
    bool Created,
    Guid? ApiKeyId,
    string? Prefix,
    string? Hint,
    string? PlainKey,
    IReadOnlyList<string> Scopes,
    // Webhook (null id when never registered / not requested and none exist)
    Guid? WebhookEndpointId,
    string? WebhookUrl,
    bool? WebhookIsActive,
    IReadOnlyList<string> WebhookEnabledEvents,
    string? WebhookSecretKey,
    string? WebhookSecretHint,
    // Owner
    bool OwnerAttached,
    string OwnerStatus,
    string? OwnerRole,
    /// <summary>Integrator product slug (e.g. aura, demo-app). Default aura.</summary>
    string ExternalProduct = "aura",
    /// <summary>External org / tenant id for that product (alias of AuraOrgId).</summary>
    string? ExternalOrgId = null);

public record ProvisionAuraWorkspaceCommand(
    string AuraOrgId,
    string DisplayName,
    string? Slug,
    string? OwnerEmail,
    string? OwnerRole,
    bool IsTestMode,
    string? KeyName,
    string? WebhookUrl,
    IReadOnlyList<string>? WebhookEnabledEvents,
    Guid? ActorUserId,
    /// <summary>External product slug. Default <c>aura</c> for backward compatibility.</summary>
    string? ExternalProduct = null) : ICommand<ProvisionAuraWorkspaceResult>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ProvisionAuraWorkspaceCommandHandler
    : ICommandHandler<ProvisionAuraWorkspaceCommand, ProvisionAuraWorkspaceResult>
{
    /// <summary>Default / Aura product slug (backward compatible). Prefer <see cref="ProductAura"/>.</summary>
    public const string ExternalProduct = "aura";
    public const string ProductAura = "aura";
    public const string PaymentsAppId = "PAYMENTS";
    public const string DefaultKeyName = "Aura bootstrap";
    public const string OwnerStatusAttached = "attached";
    public const string OwnerStatusUserNotFound = "user_not_found";
    public const string OwnerStatusNotRequested = "not_requested";
    public const string DefaultOwnerRole = "ADMIN";
    public const int MaxExternalOrgIdLength = 128;
    public const int MaxExternalProductLength = 64;

    /// <summary>Default Connect event filter when provision creates a webhook without explicit events.</summary>
    public static readonly IReadOnlyList<string> DefaultConnectWebhookEvents =
    [
        "payment.completed",
        "payment.failed"
    ];

    private static readonly HashSet<string> AllowedOwnerRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADMIN",
        "SUPER_ADMIN"
    };

    private static readonly Regex ProductSlugPattern = new(
        @"^[a-z][a-z0-9_-]{0,62}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IOneRepository _repository;
    private readonly ITokenGeneratorService _tokenGenerator;
    private readonly IEventBus _eventBus;

    public ProvisionAuraWorkspaceCommandHandler(
        IOneRepository repository,
        ITokenGeneratorService tokenGenerator,
        [FromKeyedServices("OneEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
        _eventBus = eventBus;
    }

    public async Task<ProvisionAuraWorkspaceResult> Handle(
        ProvisionAuraWorkspaceCommand request,
        CancellationToken ct)
    {
        var product = NormalizeExternalProduct(request.ExternalProduct);
        var externalOrgId = NormalizeExternalOrgId(request.AuraOrgId, product);
        var displayName = (request.DisplayName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            throw new InvalidOperationException("display_name is required.");
        }

        var ownerRole = NormalizeOwnerRole(request.OwnerRole);
        var webhookUrl = NormalizeOptionalWebhookUrl(request.WebhookUrl);
        var webhookEvents = ResolveWebhookEnabledEvents(request.WebhookEnabledEvents);

        var existing = await _repository.GetByExternalRefAsync(product, externalOrgId, ct);
        if (existing is not null)
        {
            return await EnsureAndBuildExistingAsync(
                existing,
                product,
                externalOrgId,
                request.OwnerEmail,
                ownerRole,
                webhookUrl,
                webhookEvents,
                ct);
        }

        var slug = await ResolveSlugAsync(request.Slug, product, externalOrgId, ct);
        var keyName = string.IsNullOrWhiteSpace(request.KeyName)
            ? DefaultKeyNameFor(product)
            : request.KeyName.Trim();

        var organization = new Organization(displayName, slug);
        organization.BindExternalRef(product, externalOrgId);
        _repository.AddOrganization(organization);

        var (ownerAttached, ownerStatus, attachedRole) = await TryAttachOwnerAsync(
            organization.Id,
            request.OwnerEmail,
            ownerRole,
            ct);

        var entitlement = new TenantAppEntitlement(organization.Id, PaymentsAppId);
        _repository.AddEntitlement(entitlement);
        await _eventBus.PublishAsync(
            new AppEntitlementGrantedIntegrationEvent(organization.Id, PaymentsAppId));

        // Mint integrator default scopes so org + entitlement + credential share one SaveChanges.
        var scopes = PlatformApiScopes.NormalizeAndValidate(
            PlatformApiScopes.Split(PlatformApiScopes.DefaultAuraIntegratorScopes));
        var tokenPair = _tokenGenerator.GenerateSecureToken(40);
        var prefix = request.IsTestMode ? "sk_test_" : "sk_live_";
        var plainKey = $"{prefix}{tokenPair.PlainToken}";
        var keyHash = _tokenGenerator.HashToken(plainKey);
        var keyHint = plainKey.Length >= 4 ? plainKey[^4..] : plainKey;

        var credential = new ApiCredential(
            organization.Id,
            keyName,
            prefix,
            keyHash,
            keyHint,
            scopes,
            request.ActorUserId);
        _repository.AddApiCredential(credential);

        string? webhookSecret = null;
        TenantWebhookEndpoint? webhookEndpoint = null;
        if (webhookUrl is not null)
        {
            webhookSecret = MintWebhookSecret();
            webhookEndpoint = new TenantWebhookEndpoint(
                organization.Id,
                webhookUrl,
                webhookSecret,
                isActive: true,
                webhookEvents);
            _repository.AddWebhookEndpoint(webhookEndpoint);
        }

        try
        {
            await _repository.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            // Concurrent first provision for same (product, org_id): re-read and return idempotent + ensure.
            var winner = await _repository.GetByExternalRefAsync(product, externalOrgId, ct);
            if (winner is null)
            {
                throw;
            }

            return await EnsureAndBuildExistingAsync(
                winner,
                product,
                externalOrgId,
                request.OwnerEmail,
                ownerRole,
                webhookUrl,
                webhookEvents,
                ct);
        }

        return BuildResult(
            organization.Id,
            organization.Slug,
            product,
            externalOrgId,
            created: true,
            credential.Id,
            credential.Prefix,
            credential.KeyHint,
            plainKey,
            PlatformApiScopes.Split(scopes),
            webhookEndpoint?.Id,
            webhookEndpoint?.Url,
            webhookEndpoint?.IsActive,
            webhookEndpoint is null
                ? Array.Empty<string>()
                : webhookEndpoint.EnabledEvents.ToList(),
            webhookSecret,
            webhookSecret is null ? null : SecretHint(webhookSecret),
            ownerAttached,
            ownerStatus,
            attachedRole);
    }

    private async Task<ProvisionAuraWorkspaceResult> EnsureAndBuildExistingAsync(
        Organization organization,
        string product,
        string externalOrgId,
        string? ownerEmail,
        string ownerRole,
        string? webhookUrl,
        IReadOnlyList<string> webhookEvents,
        CancellationToken ct)
    {
        var keys = await _repository.ListApiCredentialsAsync(organization.Id, ct);
        var defaultKeyName = DefaultKeyNameFor(product);
        var bootstrap = keys
            .Where(k => k.IsActive)
            .OrderBy(k => k.CreatedAt)
            .FirstOrDefault(k =>
                string.Equals(k.Name, defaultKeyName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(k.Name, DefaultKeyName, StringComparison.OrdinalIgnoreCase)
                || PlatformApiScopes.HasScope(k.Scopes, PlatformApiScopes.PaymentsCheckoutsWrite));

        // Owner heal: attach if requested and user exists and not already a member.
        var (ownerAttached, ownerStatus, attachedRole) = await EnsureOwnerAsync(
            organization.Id,
            ownerEmail,
            ownerRole,
            ct);

        // Webhook: exact URL match → metadata no secret; missing + URL given → create once.
        Guid? webhookId = null;
        string? webhookUrlOut = null;
        bool? webhookActive = null;
        IReadOnlyList<string> webhookEnabled = Array.Empty<string>();
        string? webhookSecret = null;
        string? webhookHint = null;
        var needsSave = false;

        var endpoints = await _repository.ListWebhookEndpointsAsync(organization.Id, ct);

        if (webhookUrl is not null)
        {
            var match = endpoints.FirstOrDefault(e =>
                string.Equals(e.Url, webhookUrl, StringComparison.Ordinal));

            if (match is not null)
            {
                webhookId = match.Id;
                webhookUrlOut = match.Url;
                webhookActive = match.IsActive;
                webhookEnabled = match.EnabledEvents.ToList();
                webhookHint = string.IsNullOrEmpty(match.SecretKey) ? null : SecretHint(match.SecretKey);
                // secret once only — never remint
            }
            else
            {
                webhookSecret = MintWebhookSecret();
                var created = new TenantWebhookEndpoint(
                    organization.Id,
                    webhookUrl,
                    webhookSecret,
                    isActive: true,
                    webhookEvents);
                _repository.AddWebhookEndpoint(created);
                needsSave = true;

                webhookId = created.Id;
                webhookUrlOut = created.Url;
                webhookActive = created.IsActive;
                webhookEnabled = created.EnabledEvents.ToList();
                webhookHint = SecretHint(webhookSecret);
            }
        }

        // Owner ensure saves immediately when membership is added. Webhook heal saves here.
        if (needsSave)
        {
            await _repository.SaveChangesAsync(ct);
        }

        return BuildResult(
            organization.Id,
            organization.Slug,
            product,
            externalOrgId,
            created: false,
            bootstrap?.Id,
            bootstrap?.Prefix,
            bootstrap?.KeyHint,
            plainKey: null,
            bootstrap is null
                ? PlatformApiScopes.Split(PlatformApiScopes.DefaultAuraIntegratorScopes)
                : PlatformApiScopes.Split(bootstrap.Scopes),
            webhookId,
            webhookUrlOut,
            webhookActive,
            webhookEnabled,
            webhookSecret,
            webhookHint,
            ownerAttached,
            ownerStatus,
            attachedRole);
    }

    private static ProvisionAuraWorkspaceResult BuildResult(
        Guid workspaceId,
        string slug,
        string product,
        string externalOrgId,
        bool created,
        Guid? apiKeyId,
        string? prefix,
        string? hint,
        string? plainKey,
        IReadOnlyList<string> scopes,
        Guid? webhookEndpointId,
        string? webhookUrl,
        bool? webhookIsActive,
        IReadOnlyList<string> webhookEnabledEvents,
        string? webhookSecretKey,
        string? webhookSecretHint,
        bool ownerAttached,
        string ownerStatus,
        string? ownerRole) =>
        new(
            workspaceId,
            slug,
            AuraOrgId: externalOrgId,
            Created: created,
            apiKeyId,
            prefix,
            hint,
            plainKey,
            scopes,
            webhookEndpointId,
            webhookUrl,
            webhookIsActive,
            webhookEnabledEvents,
            webhookSecretKey,
            webhookSecretHint,
            ownerAttached,
            ownerStatus,
            ownerRole,
            ExternalProduct: product,
            ExternalOrgId: externalOrgId);

    private async Task<(bool Attached, string Status, string? Role)> TryAttachOwnerAsync(
        Guid organizationId,
        string? ownerEmail,
        string ownerRole,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ownerEmail))
        {
            return (false, OwnerStatusNotRequested, null);
        }

        var owner = await _repository.GetUserByEmailAsync(ownerEmail.Trim(), ct);
        if (owner is null)
        {
            return (false, OwnerStatusUserNotFound, null);
        }

        // Create path: org is new so membership cannot exist yet.
        _repository.AddTenantMembership(new TenantMembership(owner.Id, organizationId, ownerRole));
        return (true, OwnerStatusAttached, ownerRole);
    }

    private async Task<(bool Attached, string Status, string? Role)> EnsureOwnerAsync(
        Guid organizationId,
        string? ownerEmail,
        string ownerRole,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ownerEmail))
        {
            return (false, OwnerStatusNotRequested, null);
        }

        var owner = await _repository.GetUserByEmailAsync(ownerEmail.Trim(), ct);
        if (owner is null)
        {
            return (false, OwnerStatusUserNotFound, null);
        }

        var existing = await _repository.GetMembershipAsync(owner.Id, organizationId, ct);
        if (existing is not null)
        {
            return (true, OwnerStatusAttached, existing.Role);
        }

        _repository.AddTenantMembership(new TenantMembership(owner.Id, organizationId, ownerRole));
        await _repository.SaveChangesAsync(ct);
        return (true, OwnerStatusAttached, ownerRole);
    }

    private string MintWebhookSecret() =>
        "whsec_" + _tokenGenerator.GenerateSecureToken(24).PlainToken;

    private static string SecretHint(string secret) =>
        secret.Length >= 4 ? secret[^4..] : secret;

    public static string DefaultKeyNameFor(string product) =>
        string.Equals(product, ProductAura, StringComparison.Ordinal)
            ? DefaultKeyName
            : $"{product} bootstrap";

    public static string NormalizeOwnerRole(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DefaultOwnerRole;
        }

        var role = raw.Trim().ToUpperInvariant();
        if (!AllowedOwnerRoles.Contains(role))
        {
            throw new InvalidOperationException(
                "owner_role must be ADMIN or SUPER_ADMIN (workspace membership, not global system admin).");
        }

        return role;
    }

    public static string? NormalizeOptionalWebhookUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return WebhookUrlValidator.NormalizeAndValidate(raw, allowHttpLoopback: true);
    }

    /// <summary>
    /// null/empty → Connect payment defaults; non-empty → caller filter.
    /// </summary>
    public static IReadOnlyList<string> ResolveWebhookEnabledEvents(IReadOnlyList<string>? events)
    {
        if (events is null || events.Count == 0)
        {
            return DefaultConnectWebhookEvents.ToList();
        }

        return events
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Product slug: lower-case [a-z][a-z0-9_-]* , default <see cref="ProductAura"/>.
    /// </summary>
    public static string NormalizeExternalProduct(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ProductAura;
        }

        var product = raw.Trim().ToLowerInvariant();
        if (product.Length > MaxExternalProductLength)
        {
            throw new InvalidOperationException(
                $"external_product must be at most {MaxExternalProductLength} characters.");
        }

        if (!ProductSlugPattern.IsMatch(product))
        {
            throw new InvalidOperationException(
                "external_product must start with a letter and contain only a-z, 0-9, _ or -.");
        }

        return product;
    }

    /// <summary>
    /// Resolves external org id. For product <c>aura</c>, requires a GUID (legacy contract).
    /// Other products accept any non-empty stable string (max 128), lowercased.
    /// </summary>
    public static string NormalizeExternalOrgId(string? raw, string product)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "external_org_id (or aura_org_id) is required.");
        }

        var trimmed = raw.Trim();
        if (string.Equals(product, ProductAura, StringComparison.Ordinal))
        {
            return NormalizeAuraOrgId(trimmed);
        }

        if (trimmed.Length > MaxExternalOrgIdLength)
        {
            throw new InvalidOperationException(
                $"external_org_id must be at most {MaxExternalOrgIdLength} characters.");
        }

        return trimmed.ToLowerInvariant();
    }

    /// <summary>Aura-only GUID normalizer (legacy name kept for tests / callers).</summary>
    public static string NormalizeAuraOrgId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("aura_org_id is required.");
        }

        var trimmed = raw.Trim();
        if (!Guid.TryParse(trimmed, out var guid))
        {
            throw new InvalidOperationException("aura_org_id must be a valid GUID.");
        }

        return guid.ToString("D").ToLowerInvariant();
    }

    private async Task<string> ResolveSlugAsync(
        string? requestedSlug,
        string product,
        string externalOrgId,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(requestedSlug))
        {
            var clean = requestedSlug.Trim().ToLowerInvariant();
            if (!await _repository.IsSlugUniqueAsync(clean, ct))
            {
                throw new InvalidOperationException(
                    "The requested workspace slug is already taken. Please choose another.");
            }

            return clean;
        }

        // {product}-{compact id} — compact, valid slug charset.
        var compact = externalOrgId.Replace("-", "", StringComparison.Ordinal);
        // Keep only slug-safe chars for non-guid ids.
        compact = Regex.Replace(compact, @"[^a-z0-9]", "", RegexOptions.IgnoreCase);
        if (compact.Length == 0)
        {
            compact = Guid.CreateVersion7().ToString("N")[..12];
        }

        var baseSlug = $"{product}-{compact[..Math.Min(12, compact.Length)]}";
        if (baseSlug.Length > 63)
        {
            baseSlug = baseSlug[..63].TrimEnd('-');
        }

        if (await _repository.IsSlugUniqueAsync(baseSlug, ct))
        {
            return baseSlug;
        }

        for (var i = 2; i <= 20; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (candidate.Length > 63)
            {
                candidate = candidate[..63].TrimEnd('-');
            }

            if (await _repository.IsSlugUniqueAsync(candidate, ct))
            {
                return candidate;
            }
        }

        // Extremely unlikely: append short random hex.
        var fallback = $"{product}-{Guid.CreateVersion7():N}"[..Math.Min(20, product.Length + 17)];
        if (!await _repository.IsSlugUniqueAsync(fallback, ct))
        {
            throw new InvalidOperationException("Unable to allocate a unique workspace slug.");
        }

        return fallback;
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        // Npgsql: 23505 unique_violation. Match by message/type without hard Npgsql dependency in Application.
        for (var e = ex; e is not null; e = e.InnerException)
        {
            var typeName = e.GetType().Name;
            if (typeName.Contains("Postgres", StringComparison.OrdinalIgnoreCase)
                && e.Message.Contains("23505", StringComparison.Ordinal))
            {
                return true;
            }

            if (e.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)
                && (e.Message.Contains("ExternalProduct", StringComparison.OrdinalIgnoreCase)
                    || e.Message.Contains("IX_Organizations_External", StringComparison.OrdinalIgnoreCase)
                    || e.Message.Contains("23505", StringComparison.Ordinal)))
            {
                return true;
            }

            // EF InMemory / generic: DbUpdateException with inner unique.
            if (typeName.Contains("DbUpdate", StringComparison.OrdinalIgnoreCase)
                && e.InnerException is not null
                && IsUniqueViolation(e.InnerException))
            {
                return true;
            }
        }

        return false;
    }
}
