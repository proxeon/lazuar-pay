// apps/lazuar-api/Modules/Ops/Application/Commands/RequestFormInputCommand.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Ops.Contracts;

namespace Modules.Ops.Application.Commands;

[AgentTool("Request a user interface form to collect missing parameters for a target command.", "CORE", "low", "SUPER_ADMIN", "ADMIN")]
public record RequestFormInputCommand(string TargetToolName, object? PartialData) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RequestFormInputCommandHandler : ICommandHandler<RequestFormInputCommand>
{
    public Task Handle(RequestFormInputCommand request, CancellationToken cancellationToken)
    {
        // This command is natively intercepted within the LlmOrchestratorService loop.
        // It is never routed through MediatR execution.
        return Task.CompletedTask;
    }
}
