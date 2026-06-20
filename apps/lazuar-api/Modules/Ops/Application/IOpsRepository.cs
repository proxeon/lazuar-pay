// apps/lazuar-api/Modules/Ops/Application/IOpsRepository.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Modules.Ops.Domain;

namespace Modules.Ops.Application;

public interface IOpsRepository
{
    Task<OpsConversation?> GetConversationByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<IEnumerable<OpsConversation>> GetConversationsAsync(Guid organizationId, int limit, int offset, CancellationToken ct = default);
    Task<OpsMessage?> GetMessageByIdAsync(Guid organizationId, Guid messageId, CancellationToken ct = default);
    Task<IEnumerable<OpsMessage>> GetMessagesAsync(Guid organizationId, Guid conversationId, CancellationToken ct = default);

    void AddConversation(OpsConversation conversation);
    void AddMessage(OpsMessage message);

    Task SaveChangesAsync(CancellationToken ct = default);
}
