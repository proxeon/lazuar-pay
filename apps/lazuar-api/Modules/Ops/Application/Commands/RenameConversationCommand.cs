using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Ops.Application.Commands;

public record RenameConversationCommand(Guid TenantId, Guid ConversationId, string NewTitle) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RenameConversationCommandHandler : ICommandHandler<RenameConversationCommand>
{
    private readonly IOpsRepository _repository;

    public RenameConversationCommandHandler(IOpsRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(RenameConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _repository.GetConversationByIdAsync(request.TenantId, request.ConversationId, cancellationToken);
        if (conversation == null)
        {
            throw new InvalidOperationException("Conversation not found.");
        }

        conversation.UpdateTitle(request.NewTitle);
        await _repository.SaveChangesAsync(cancellationToken);
    }
}
