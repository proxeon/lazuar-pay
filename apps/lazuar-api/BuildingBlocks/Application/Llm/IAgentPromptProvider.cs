namespace BuildingBlocks.Application.Llm;

public interface IAgentPromptProvider
{
    string GetAppId();
    string GetSystemPromptRules();
}
