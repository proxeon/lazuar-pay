using System;
using System.Collections.Generic;
using System.Linq;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public partial class ProvisionAuraWorkspaceCommandHandler
{
    private (ApiCredential Credential, string PlainKey, IReadOnlyList<string> ScopesList) MintBootstrapCredential(
        Guid organizationId,
        string keyName,
        bool isTestMode,
        Guid? actorUserId)
    {
        // Mint integrator default scopes so org + entitlement + credential share one SaveChanges.
        var scopes = PlatformApiScopes.NormalizeAndValidate(
            PlatformApiScopes.Split(PlatformApiScopes.DefaultAuraIntegratorScopes));
        var tokenPair = _tokenGenerator.GenerateSecureToken(40);
        var prefix = isTestMode ? "sk_test_" : "sk_live_";
        var plainKey = $"{prefix}{tokenPair.PlainToken}";
        var keyHash = _tokenGenerator.HashToken(plainKey);
        var keyHint = plainKey.Length >= 4 ? plainKey[^4..] : plainKey;

        var credential = new ApiCredential(
            organizationId,
            keyName,
            prefix,
            keyHash,
            keyHint,
            scopes,
            actorUserId);
        _repository.AddApiCredential(credential);

        return (credential, plainKey, PlatformApiScopes.Split(scopes));
    }

    private static ApiCredential? SelectBootstrapCredential(
        IReadOnlyList<ApiCredential> keys,
        string product)
    {
        var defaultKeyName = DefaultKeyNameFor(product);
        return keys
            .Where(k => k.IsActive)
            .OrderBy(k => k.CreatedAt)
            .FirstOrDefault(k =>
                string.Equals(k.Name, defaultKeyName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(k.Name, DefaultKeyName, StringComparison.OrdinalIgnoreCase)
                || PlatformApiScopes.HasScope(k.Scopes, PlatformApiScopes.PaymentsCheckoutsWrite));
    }
}
