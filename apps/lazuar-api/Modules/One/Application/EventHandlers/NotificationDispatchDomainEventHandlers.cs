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
        var resetLink = $"{_linkService.GetClientBaseUrl()}/reset-password?email={Uri.EscapeDataString(notification.Email)}&token={notification.PlainToken}";
        var subject = "Password Reset Request";
        
        var rawMarkdown = $@"Hi,

You requested a password reset. Click the link below to set a new password:

[Reset Password]({resetLink})

If you did not request this, please ignore this email.";

        var htmlBody = MarkdownParser.ToHtml(rawMarkdown);

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            _systemTenantId, notification.Email, null, subject, htmlBody, null, "EMAIL"));
    }

    public async Task Handle(EmailVerificationRequestedDomainEvent notification, CancellationToken ct)
    {
        var verifyLink = $"{_linkService.GetClientBaseUrl()}/verify-email?email={Uri.EscapeDataString(notification.Email)}&token={notification.PlainToken}";
        var subject = "Verify your email address";

        var rawMarkdown = $@"Hi {notification.Name},

Welcome to Lazuar! Please verify your email address by clicking the link below:

[Verify Email]({verifyLink})";

        var htmlBody = MarkdownParser.ToHtml(rawMarkdown);

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            _systemTenantId, notification.Email, null, subject, htmlBody, null, "EMAIL"));
    }

    public async Task Handle(WorkspaceInvitationCreatedDomainEvent notification, CancellationToken ct)
    {
        var acceptLink = $"{_linkService.GetOpsBaseUrl()}/accept-invite?token={notification.PlainToken}";
        var subject = "You've been invited to join a workspace";

        var rawMarkdown = $@"Hi,

You have been invited to join a workspace as a **{notification.Role}**.

[Accept Invitation]({acceptLink})";

        var htmlBody = MarkdownParser.ToHtml(rawMarkdown);

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            notification.OrganizationId, notification.Email, null, subject, htmlBody, null, "EMAIL"));
    }
}
