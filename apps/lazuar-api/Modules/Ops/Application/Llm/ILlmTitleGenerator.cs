using System.Threading.Tasks;

namespace Modules.Ops.Application.Llm;

public interface ILlmTitleGenerator
{
    Task<string> GenerateAsync(string contentContext);
    string GenerateFallback(string content);
}
