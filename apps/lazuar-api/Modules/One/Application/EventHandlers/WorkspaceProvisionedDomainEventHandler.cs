using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Modules.Messaging.Contracts;
using Modules.One.Domain.Events;

namespace Modules.One.Application.EventHandlers;

public class WorkspaceProvisionedDomainEventHandler : INotificationHandler<WorkspaceProvisionedDomainEvent>
{
    private readonly IEventBus _eventBus;

    public WorkspaceProvisionedDomainEventHandler([FromKeyedServices("OneEventBus")] IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task Handle(WorkspaceProvisionedDomainEvent notification, CancellationToken ct)
    {
        string subject = $"Welcome to {notification.WorkspaceName}!";
        string body;

        // If a password was generated, this is a brand new user to the platform
        if (!string.IsNullOrEmpty(notification.GeneratedPassword))
        {
            body = $"Hi {notification.OwnerName},<br><br>" +
                   $"Your workspace <strong>{notification.WorkspaceName}</strong> is ready.<br><br>" +
                   $"You can log in at <a href=\"http://localhost:3001/login\">Lazuar One</a> using this email address.<br><br>" +
                   $"Your temporary password is: <br><br><code>{notification.GeneratedPassword}</code><br><br>" +
                   $"Please log in and change your password immediately.<br><br>" +
                   $"— The Lazuar Team";
        }
        else
        {
            // The user already exists in the platform (they just purchased a second workspace)
            body = $"Hi {notification.OwnerName},<br><br>" +
                   $"Your new workspace <strong>{notification.WorkspaceName}</strong> has been successfully provisioned.<br><br>" +
                   $"You can access it by logging in to <a href=\"http://localhost:3001/login\">Lazuar One</a> with your existing credentials.<br><br>" +
                   $"— The Lazuar Team";
        }

        var dispatchEvent = new DispatchMessageIntegrationEvent(
            OrganizationId: notification.OrganizationId,
            ToEmail: notification.OwnerEmail,
            ToPhone: null,
            Subject: subject,
            HtmlBody: body,
            Channel: "EMAIL"
        );

        // Publish to the local outbox, destined for the Messaging module
        await _eventBus.PublishAsync(dispatchEvent);
    }
}
