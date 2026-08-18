// apps/lazuar-api/Modules/One/Application/IOneRepository.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Modules.One.Domain;

namespace Modules.One.Application;

public interface IOneRepository
{
    void AddOrganization(Organization organization);
    Task<Organization?> GetOrganizationByIdAsync(Guid id, CancellationToken ct = default);
    Task<Organization?> GetByExternalRefAsync(string product, string externalOrgId, CancellationToken ct = default);
    Task<bool> IsSlugUniqueAsync(string slug, CancellationToken ct = default);

    void AddTenantMembership(TenantMembership membership);
    void RemoveTenantMembership(TenantMembership membership);
    void AddEntitlement(TenantAppEntitlement entitlement);

    Task<TenantAppEntitlement?> GetEntitlementAsync(Guid organizationId, string appId, CancellationToken ct = default);
    Task<bool> HasMembershipAsync(Guid globalUserId, Guid organizationId, CancellationToken ct = default);
    Task<TenantMembership?> GetMembershipAsync(Guid globalUserId, Guid organizationId, CancellationToken ct = default);
    Task<int> CountManagingMembersAsync(Guid organizationId, CancellationToken ct = default);

    Task<GlobalUser?> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<GlobalUser?> GetUserByEmailAsync(string email, CancellationToken ct = default);
    void AddGlobalUser(GlobalUser user);

    void AddWorkspaceInvitation(WorkspaceInvitation invitation);
    Task<WorkspaceInvitation?> GetPendingInvitationAsync(Guid organizationId, string email, CancellationToken ct = default);
    Task<WorkspaceInvitation?> GetInvitationByHashAsync(string hash, CancellationToken ct = default);
    Task<WorkspaceInvitation?> GetInvitationByIdAsync(Guid id, CancellationToken ct = default);

    Task<TenantWebhookEndpoint?> GetWebhookEndpointAsync(Guid organizationId, CancellationToken ct = default);
    Task<TenantWebhookEndpoint?> GetWebhookEndpointByIdAsync(Guid endpointId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantWebhookEndpoint>> ListWebhookEndpointsAsync(Guid organizationId, CancellationToken ct = default);
    void AddWebhookEndpoint(TenantWebhookEndpoint endpoint);

    Task<WebhookDeliveryOutbox?> GetWebhookDeliveryAsync(
        Guid organizationId, Guid deliveryId, CancellationToken ct = default);
    void AddWebhookDelivery(WebhookDeliveryOutbox delivery);

    Task<ApiCredential?> GetApiCredentialAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ApiCredential>> ListApiCredentialsAsync(Guid organizationId, CancellationToken ct = default);
    void AddApiCredential(ApiCredential credential);

    Task SaveChangesAsync(CancellationToken ct = default);
}
