using System.Threading;
using System.Threading.Tasks;
using Lazuar.ApiTypes;

namespace Modules.Ops.Application.Services;

public interface ILlmOrchestratorService
{
    Task<ChatResponseDto> ProcessChatAsync(string userMessage, CancellationToken ct = default);
}
