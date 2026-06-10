using System.Threading.Tasks;

namespace BuildingBlocks.Application.Llm;

public interface ILlmTitleGenerator
{
    Task<string> GenerateAsync(string contentContext);
    string GenerateFallback(string content);
}
