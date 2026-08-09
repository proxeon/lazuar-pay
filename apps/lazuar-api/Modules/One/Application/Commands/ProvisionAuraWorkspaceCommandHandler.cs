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

public partial class ProvisionAuraWorkspaceCommandHandler
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

        var organization = CreateBoundOrganization(displayName, slug, product, externalOrgId);

        var (ownerAttached, ownerStatus, attachedRole) = await TryAttachOwnerAsync(
            organization.Id,
            request.OwnerEmail,
            ownerRole,
            ct);

        await GrantPaymentsEntitlementAsync(organization.Id);

        var (credential, plainKey, scopesList) = MintBootstrapCredential(
            organization.Id,
            keyName,
            request.IsTestMode,
            request.ActorUserId);

        var (webhookEndpoint, webhookSecret) = TryCreateWebhookEndpoint(
            organization.Id,
            webhookUrl,
            webhookEvents);

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
            scopesList,
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
        var bootstrap = SelectBootstrapCredential(keys, product);

        // Owner heal: attach if requested and user exists and not already a member.
        var (ownerAttached, ownerStatus, attachedRole) = await EnsureOwnerAsync(
            organization.Id,
            ownerEmail,
            ownerRole,
            ct);

        var webhookState = await EnsureWebhookAsync(
            organization.Id,
            webhookUrl,
            webhookEvents,
            ct);

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
            webhookState.Id,
            webhookState.Url,
            webhookState.IsActive,
            webhookState.EnabledEvents,
            webhookState.SecretKey,
            webhookState.SecretHint,
            ownerAttached,
            ownerStatus,
            attachedRole);
    }
}
