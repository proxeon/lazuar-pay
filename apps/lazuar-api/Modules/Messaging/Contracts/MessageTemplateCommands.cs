using System;
using BuildingBlocks.Application;

namespace Modules.Messaging.Contracts;

public record UpdateMessageTemplateCommand(Guid OrganizationId, Guid TemplateId, string Subject, string Body): ICommand {
    public Guid Id {
        get;
        init;
    } = Guid.CreateVersion7();
}

public record ResetMessageTemplateCommand(Guid OrganizationId, Guid TemplateId): ICommand {
    public Guid Id {
        get;
        init;
    } = Guid.CreateVersion7();
}

public record SendTestReminderCommand(Guid OrganizationId, string TemplateName, string ? Channel): ICommand {
    public Guid Id {
        get;
        init;
    } = Guid.CreateVersion7();
}
