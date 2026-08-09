using System;
using System.Threading;
using System.Threading.Tasks;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public partial class ProvisionAuraWorkspaceCommandHandler
{
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
}
