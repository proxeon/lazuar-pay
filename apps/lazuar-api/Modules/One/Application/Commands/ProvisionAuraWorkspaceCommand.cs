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
    IReadOnlyList<string> Scopes);

public record ProvisionAuraWorkspaceCommand(
    string AuraOrgId,
    string DisplayName,
    string? Slug,
    string? OwnerEmail,
    bool IsTestMode,
    string? KeyName,
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

        var existing = await _repository.GetByExternalRefAsync(ExternalProduct, auraOrgId, ct);
        if (existing is not null)
        {
            return await BuildExistingResultAsync(existing, auraOrgId, ct);
        }

        var slug = await ResolveSlugAsync(request.Slug, auraOrgId, ct);
        var keyName = string.IsNullOrWhiteSpace(request.KeyName)
            ? DefaultKeyName
            : request.KeyName.Trim();

        var organization = new Organization(displayName, slug);
        organization.BindExternalRef(ExternalProduct, auraOrgId);
        _repository.AddOrganization(organization);

        // Optional: attach existing GlobalUser as ADMIN (do not create users).
        if (!string.IsNullOrWhiteSpace(request.OwnerEmail))
        {
            var owner = await _repository.GetUserByEmailAsync(request.OwnerEmail, ct);
            if (owner is not null)
            {
                _repository.AddTenantMembership(
                    new TenantMembership(owner.Id, organization.Id, "ADMIN"));
            }
        }

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

        try
        {
            await _repository.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            // Concurrent first provision for same aura_org_id: re-read and return idempotent.
            var winner = await _repository.GetByExternalRefAsync(ExternalProduct, auraOrgId, ct);
            if (winner is null)
            {
                throw;
            }

            return await BuildExistingResultAsync(winner, auraOrgId, ct);
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
            PlatformApiScopes.Split(scopes));
    }

    private async Task<ProvisionAuraWorkspaceResult> BuildExistingResultAsync(
        Organization organization,
        string auraOrgId,
        CancellationToken ct)
    {
        var keys = await _repository.ListApiCredentialsAsync(organization.Id, ct);
        var bootstrap = keys
            .Where(k => k.IsActive)
            .OrderBy(k => k.CreatedAt)
            .FirstOrDefault(k =>
                string.Equals(k.Name, DefaultKeyName, StringComparison.OrdinalIgnoreCase)
                || PlatformApiScopes.HasScope(k.Scopes, PlatformApiScopes.PaymentsCheckoutsWrite));

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
                : PlatformApiScopes.Split(bootstrap.Scopes));
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
