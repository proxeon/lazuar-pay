// apps/lazuar-api/Modules/Community/Application/Llm/CommunityPromptProvider.cs
using BuildingBlocks.Application.Llm;

namespace Modules.Community.Application.Llm;

public class CommunityPromptProvider : IAgentPromptProvider
{
    public string GetAppId() => "COMMUNITY";

    public string GetSystemPromptRules()
    {
        return "**COMMUNITY MODULE RULES**:\n" +
               "- NEVER use ListPlanSubscribers to retrieve IDs for the purpose of sending messages. If you need to contact multiple users (e.g., a whole class or everyone who is PAST_DUE), ALWAYS use the SendBroadcastCommand.\n" +
               "- Use search tools to find exact GUID identifiers for Subscribers or Plans before executing any write commands. NEVER guess or hallucinate a Guid!\n" +
               "- Plans no longer support rich descriptions, methodologies, or FAQs. Do not attempt to add or generate marketing copy for plans.";
    }
}
