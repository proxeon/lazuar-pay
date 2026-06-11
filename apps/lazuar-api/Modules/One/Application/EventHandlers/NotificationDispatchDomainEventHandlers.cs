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
    INotificationHandler<WorkspaceInvitationCreatedDomainEvent>,
    INotificationHandler<AppAccessRequestedDomainEvent>,
    INotificationHandler<AppAccessApprovedDomainEvent>
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
        var resetLink = $"{_linkService.GetClientBaseUrl()}/reset-password?email={Uri.EscapeDataString(notification.Email)}&token={notification.PlainToken}";
        var subject = "Password Reset Request";
        var body = $"Hi,<br><br>You requested a password reset. Click here to set a new password: <a href=\"{resetLink}\">Reset Password</a><br><br>If you did not request this, please ignore this email.";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            _systemTenantId, notification.Email, null, subject, body, "EMAIL"));
    }

    public async Task Handle(EmailVerificationRequestedDomainEvent notification, CancellationToken ct)
    {
        var verifyLink = $"{_linkService.GetClientBaseUrl()}/verify-email?email={Uri.EscapeDataString(notification.Email)}&token={notification.PlainToken}";
        var subject = "Verify your email address";
        var body = $"Hi {notification.Name},<br><br>Welcome to Lazuar! Please verify your email address by clicking the link below:<br><br><a href=\"{verifyLink}\">Verify Email</a>";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            _systemTenantId, notification.Email, null, subject, body, "EMAIL"));
    }

    public async Task Handle(WorkspaceInvitationCreatedDomainEvent notification, CancellationToken ct)
    {
        var acceptLink = $"{_linkService.GetClientBaseUrl()}/accept-invite?token={notification.PlainToken}";
        var subject = "You've been invited to join a workspace";
        var body = $"Hi,<br><br>You have been invited to join a workspace as {notification.Role}.<br><br><a href=\"{acceptLink}\">Accept Invitation</a>";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            notification.OrganizationId, notification.Email, null, subject, body, "EMAIL"));
    }

    public async Task Handle(AppAccessRequestedDomainEvent notification, CancellationToken ct)
    {
        var subject = "We've received your application";
        var body = $"Hi,<br><br>Thank you for applying to the Lazuar Ecosystem. Your request for the following apps is under review:<br><br><b>{string.Join(", ", notification.RequestedApps)}</b><br><br>We will notify you once an administrator approves your workspace.";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            _systemTenantId, "user-placeholder@will-be-fixed-by-repo.com", null, subject, body, "EMAIL"));
    }

    public async Task Handle(AppAccessApprovedDomainEvent notification, CancellationToken ct)
    {
        var loginLink = $"{_linkService.GetClientBaseUrl()}/login";
        var subject = "Your workspace is ready!";
        var body = $"Hi,<br><br>Great news! Your workspace has been provisioned and your requested applications are now active.<br><br><a href=\"{loginLink}\">Access your Launchpad</a>";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            _systemTenantId, "user-placeholder@will-be-fixed-by-repo.com", null, subject, body, "EMAIL"));
    }
}
