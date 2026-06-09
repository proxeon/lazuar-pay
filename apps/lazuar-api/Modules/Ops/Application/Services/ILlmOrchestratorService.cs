using System.Threading;
using System.Threading.Tasks;

namespace Modules.Ops.Application.Services;

public interface ILlmOrchestratorService
{
    Task<string> ProcessChatAsync(string userMessage, CancellationToken ct = default);
}
