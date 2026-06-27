using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Communications.Contracts.Commands;

namespace Modules.Communications.Application.Commands;

public class SendBroadcastCommandHandler : ICommandHandler<SendBroadcastCommand, Guid>
{
    public Task<Guid> Handle(SendBroadcastCommand request, CancellationToken ct)
    {
        // Broadcast entity was removed during Phase 1 cleanup to streamline the app.
        // We will execute a synchronous loop in a future phase if Broadcasts are reintroduced to the CaaS model.
        throw new NotImplementedException("Broadcast functionality is currently retired in the CaaS model.");
    }
}
