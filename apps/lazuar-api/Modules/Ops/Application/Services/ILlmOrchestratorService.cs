using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lazuar.ApiTypes;

namespace Modules.Ops.Application.Services;

public interface ILlmOrchestratorService
{
    Task<ChatResponseDto> ProcessChatAsync(string userMessage, string? conversationId = null, CancellationToken ct = default);
    
    IAsyncEnumerable<ChatStreamChunkDto> ProcessChatStreamAsync(string userMessage, string? conversationId = null, CancellationToken ct = default);
}
