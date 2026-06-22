using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Modules.Messaging.Contracts;
using Modules.One.Domain.Events;

namespace Modules.One.Application.EventHandlers;

public class NotificationDispatchDomainEventHandlers :
    INotificationHandler<PasswordResetRequestedDomainEvent>,
    INotificationHandler<EmailVerificationRequestedDomainEvent>,
    INotificationHandler<WorkspaceInvitationCreatedDomainEvent>
{
    private readonly IEventBus _eventBus;
    private readonly IOneLinkService _linkService;
    private readonly Guid _systemTenantId = Guid.Empty;

    public NotificationDispatchDomainEventHandlers(
        [FromKeyedServices("OneEventBus")] IEventBus eventBus,
        IOneLinkService linkService)
    {
        _eventBus = eventBus;
        _linkService = linkService;
    }

    public async Task Handle(PasswordResetRequestedDomainEvent notification, CancellationToken ct)
    {
        var resetLink = $"{_linkService.GetAuthUrl()}/reset-password?email={Uri.EscapeDataString(notification.Email)}&token={notification.PlainToken}";
        var subject = "Password Reset Request";
        var body = $"Hi,<br><br>You requested a password reset. Click here to set a new password: <a href=\"{resetLink}\">Reset Password</a><br><br>If you did not request this, please ignore this email.";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            _systemTenantId, notification.Email, null, subject, body, "EMAIL"));
    }

    public async Task Handle(EmailVerificationRequestedDomainEvent notification, CancellationToken ct)
    {
        var verifyLink = $"{_linkService.GetAuthUrl()}/verify-email?email={Uri.EscapeDataString(notification.Email)}&token={notification.PlainToken}";
        var subject = "Verify your email address";
        var body = $"Hi {notification.Name},<br><br>Welcome to Lazuar! Please verify your email address by clicking the link below:<br><br><a href=\"{verifyLink}\">Verify Email</a>";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            _systemTenantId, notification.Email, null, subject, body, "EMAIL"));
    }

    public async Task Handle(WorkspaceInvitationCreatedDomainEvent notification, CancellationToken ct)
    {
        var acceptLink = $"{_linkService.GetAuthUrl()}/accept-invite?token={notification.PlainToken}";
        var subject = "You've been invited to join a workspace";
        var body = $"Hi,<br><br>You have been invited to join a workspace as {notification.Role}.<br><br><a href=\"{acceptLink}\">Accept Invitation</a>";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            notification.OrganizationId, notification.Email, null, subject, body, "EMAIL"));
    }
}
