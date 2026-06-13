using BuildingBlocks.Application.Llm;

namespace Modules.Community.Application.Llm;

public class CommunityPromptProvider : IAgentPromptProvider
{
    public string GetAppId() => "COMMUNITY";

    public string GetSystemPromptRules()
    {
        return "**COMMUNITY MODULE RULES**:\n" +
               "- When executing bulk actions (Broadcasts), rely on the dedicated batch tools. Never attempt to loop through individual subscriber tools to send bulk messages, as this will violate system timeout boundaries.\n" +
               "- Use search tools to find exact GUID identifiers for Subscribers or Plans before executing any write commands. NEVER guess or hallucinate a Guid!";
    }
}
