using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Modules.One.Application;
using Modules.One.Application.Commands;
using Modules.One.Contracts;
using Modules.One.Domain;

namespace Modules.One.Infrastructure.Services;

public class TenantWebhookRegistry : ITenantWebhookRegistry
{
    private readonly IMediator _mediator;
    private readonly IOneRepository _repository;

    public TenantWebhookRegistry(IMediator mediator, IOneRepository repository)
    {
        _mediator = mediator;
        _repository = repository;
    }

    public async Task<TenantWebhookRegisterResult> RegisterAsync(
        Guid organizationId,
        string url,
        IReadOnlyList<string>? enabledEvents,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new CreateWebhookEndpointCommand(organizationId, url, IsActive: true, EnabledEvents: enabledEvents),
            ct);

        return new TenantWebhookRegisterResult(
            result.Id,
            result.Url,
            result.IsActive,
            result.EnabledEvents);
    }

    public async Task<IReadOnlyList<TenantWebhookEndpointSnapshot>> ListAsync(
        Guid organizationId,
        CancellationToken ct = default)
    {
        var endpoints = await _repository.ListWebhookEndpointsAsync(organizationId, ct);
        return endpoints.Select(ToSnapshot).ToList();
    }

    public async Task<TenantWebhookEndpointSnapshot?> GetByIdAsync(
        Guid organizationId,
        Guid endpointId,
        CancellationToken ct = default)
    {
        var endpoint = await _repository.GetWebhookEndpointByIdAsync(endpointId, ct);
        if (endpoint == null || endpoint.OrganizationId != organizationId)
            return null;

        return ToSnapshot(endpoint);
    }

    public async Task DisableAsync(
        Guid organizationId,
        Guid endpointId,
        CancellationToken ct = default)
    {
        var endpoint = await _repository.GetWebhookEndpointByIdAsync(endpointId, ct);
        if (endpoint == null || endpoint.OrganizationId != organizationId)
            return;

        endpoint.Disable();
        await _repository.SaveChangesAsync(ct);
    }

    public async Task DisableByUrlAsync(
        Guid organizationId,
        string url,
        CancellationToken ct = default)
    {
        string normalized;
        try
        {
            normalized = WebhookUrlValidator.NormalizeAndValidate(url, allowHttpLoopback: true);
        }
        catch (Exception)
        {
            normalized = url.Trim();
        }

        var endpoints = await _repository.ListWebhookEndpointsAsync(organizationId, ct);
        var match = endpoints.FirstOrDefault(e =>
            e.IsActive && string.Equals(e.Url, normalized, StringComparison.Ordinal));
        if (match == null)
            return;

        match.Disable();
        await _repository.SaveChangesAsync(ct);
    }

    private static TenantWebhookEndpointSnapshot ToSnapshot(TenantWebhookEndpoint endpoint) =>
        new(
            endpoint.Id,
            endpoint.Url,
            endpoint.IsActive,
            endpoint.EnabledEvents.ToList(),
            endpoint.CreatedAt);
}
