using System;
using System.Collections.Generic;
using System.Linq;
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
    string? OwnerRole);

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
    Guid? ActorUserId) : ICommand<ProvisionAuraWorkspaceResult>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ProvisionAuraWorkspaceCommandHandler
    : ICommandHandler<ProvisionAuraWorkspaceCommand, ProvisionAuraWorkspaceResult>
{
    public const string ExternalProduct = "aura";
    public const string PaymentsAppId = "PAYMENTS";
    public const string DefaultKeyName = "Aura bootstrap";
    public const string OwnerStatusAttached = "attached";
    public const string OwnerStatusUserNotFound = "user_not_found";
    public const string OwnerStatusNotRequested = "not_requested";
    public const string DefaultOwnerRole = "ADMIN";

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
        var auraOrgId = NormalizeAuraOrgId(request.AuraOrgId);
        var displayName = (request.DisplayName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            throw new InvalidOperationException("display_name is required.");
        }

        var ownerRole = NormalizeOwnerRole(request.OwnerRole);
        var webhookUrl = NormalizeOptionalWebhookUrl(request.WebhookUrl);
        var webhookEvents = ResolveWebhookEnabledEvents(request.WebhookEnabledEvents);

        var existing = await _repository.GetByExternalRefAsync(ExternalProduct, auraOrgId, ct);
        if (existing is not null)
        {
            return await EnsureAndBuildExistingAsync(
                existing,
                auraOrgId,
                request.OwnerEmail,
                ownerRole,
                webhookUrl,
                webhookEvents,
                ct);
        }

        var slug = await ResolveSlugAsync(request.Slug, auraOrgId, ct);
        var keyName = string.IsNullOrWhiteSpace(request.KeyName)
            ? DefaultKeyName
            : request.KeyName.Trim();

        var organization = new Organization(displayName, slug);
        organization.BindExternalRef(ExternalProduct, auraOrgId);
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

        // Mint Aura default scopes inline so org + entitlement + credential share one SaveChanges.
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
            // Concurrent first provision for same aura_org_id: re-read and return idempotent + ensure.
            var winner = await _repository.GetByExternalRefAsync(ExternalProduct, auraOrgId, ct);
            if (winner is null)
            {
                throw;
            }

            return await EnsureAndBuildExistingAsync(
                winner,
                auraOrgId,
                request.OwnerEmail,
                ownerRole,
                webhookUrl,
                webhookEvents,
                ct);
        }

        return new ProvisionAuraWorkspaceResult(
            organization.Id,
            organization.Slug,
            auraOrgId,
            Created: true,
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
        string auraOrgId,
        string? ownerEmail,
        string ownerRole,
        string? webhookUrl,
        IReadOnlyList<string> webhookEvents,
        CancellationToken ct)
    {
        var keys = await _repository.ListApiCredentialsAsync(organization.Id, ct);
        var bootstrap = keys
            .Where(k => k.IsActive)
            .OrderBy(k => k.CreatedAt)
            .FirstOrDefault(k =>
                string.Equals(k.Name, DefaultKeyName, StringComparison.OrdinalIgnoreCase)
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

        return new ProvisionAuraWorkspaceResult(
            organization.Id,
            organization.Slug,
            auraOrgId,
            Created: false,
            bootstrap?.Id,
            bootstrap?.Prefix,
            bootstrap?.KeyHint,
            PlainKey: null,
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

    private async Task<string> ResolveSlugAsync(string? requestedSlug, string auraOrgId, CancellationToken ct)
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

        // aura-{first 10 hex of guid without dashes} — compact, valid slug charset.
        var compact = auraOrgId.Replace("-", "", StringComparison.Ordinal);
        var baseSlug = "aura-" + compact[..Math.Min(12, compact.Length)];

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
        var fallback = $"aura-{Guid.CreateVersion7():N}"[..20];
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
