using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Modules.Communications.Domain.Aggregates;

namespace Modules.Communications.Application;

public interface ICommunicationsRepository
{
    Task<MessageTemplate?> GetTemplateByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<MessageTemplate?> GetTemplateByNameAsync(Guid organizationId, string name, CancellationToken ct = default);
    void AddTemplate(MessageTemplate template);

    Task<Broadcast?> GetBroadcastByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    void AddBroadcast(Broadcast broadcast);
    Task SaveChangesAsync(CancellationToken ct = default);
}
