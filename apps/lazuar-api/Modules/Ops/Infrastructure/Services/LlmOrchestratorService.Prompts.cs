// apps/lazuar-api/Modules/Ops/Infrastructure/Services/LlmOrchestratorService.Prompts.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Modules.Ops.Application.Commands;
using Modules.Ops.Application.Services;
using Modules.Ops.Domain;
using OpenAI.Chat;

namespace Modules.Ops.Infrastructure.Services;

public partial class LlmOrchestratorService
{
    private List<ChatMessage> BuildInitialMessages(Guid tenantId, IEnumerable<OpsMessage> history, string currentMessage, IEnumerable<string> activeApps)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are Lazuar Ops, a highly capable internal operations agent.");
        sb.AppendLine($"The current Target OrganizationId is {tenantId}.");
        sb.AppendLine("**CRITICAL RULE 1**: You must ALWAYS use search tools to find exact GUID identifiers before executing any write commands. NEVER guess or hallucinate a Guid!");
        sb.AppendLine("**CRITICAL RULE 2**: You MUST use the native tool calling API. NEVER output raw JSON or fake system messages in your text response.");
        sb.AppendLine("**CRITICAL RULE 3**: NEVER guess or manually construct URLs. You MUST ALWAYS use the appropriate tool to retrieve exact URLs.");
        sb.AppendLine("**CRITICAL RULE 4**: When you lack the required parameters to execute a write command, DO NOT guess or ask the user in plain text. Instead, strictly call the `RequestFormInputCommand` tool, providing the target tool name and any data you already know. The system will render a secure form for the user.");
        sb.AppendLine("**CRITICAL RULE 5**: When executing bulk actions (Broadcasts) or financial lookups (Global Ledger), rely on the dedicated batch tools. Never attempt to loop through individual subscriber tools to send bulk messages, as this will violate system timeout boundaries.");
        sb.AppendLine("**CRITICAL RULE 6**: When discussing revenue, strictly differentiate between 'Gross Revenue' (catalog sales) and 'Net revenue' (P&L after booked gateway fees and tax). Do not call that figure cash in the bank. Always remind the user of 'Tax Liabilities' (SST/VAT) that are owed to the government and should not be counted as profit. Use the GetFinancialHealthAgentQuery tool for accurate ledger-based metrics.");
        sb.AppendLine("**CRITICAL RULE 7**: If a tool returns an error or empty result, DO NOT execute the exact same tool with the exact same parameters again. Try a different approach or ask the user for clarification.");

        var activeAppsSet = new HashSet<string>(activeApps, StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _promptProviders)
        {
            if (activeAppsSet.Contains(provider.GetAppId()))
            {
                sb.AppendLine();
                sb.AppendLine(provider.GetSystemPromptRules());
            }
        }

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(sb.ToString())
        };

        foreach (var msg in history)
        {
            if (string.IsNullOrWhiteSpace(msg.Content) && string.IsNullOrWhiteSpace(msg.ProposedActionJson) && string.IsNullOrWhiteSpace(msg.UiRequestJson))
                continue;

            var content = msg.Content;

            if (msg.Role == "user")
            {
                messages.Add(new UserChatMessage(content));
            }
            else if (msg.Role == "assistant")
            {
                messages.Add(new AssistantChatMessage(content));
                if (!string.IsNullOrEmpty(msg.ProposedActionJson))
                {
                    messages.Add(new SystemChatMessage($"[System Log: You invoked a tool with payload: {msg.ProposedActionJson}]"));
                }
                if (!string.IsNullOrEmpty(msg.UiRequestJson))
                {
                    messages.Add(new SystemChatMessage($"[System Log: You requested a UI form collection: {msg.UiRequestJson}]"));
                }
            }
            else if (msg.Role == "system")
            {
                messages.Add(new SystemChatMessage(content));
            }
        }

        messages.Add(new UserChatMessage(currentMessage));
        return messages;
    }

    private ChatCompletionOptions BuildChatOptions(IEnumerable<string> activeApps)
    {
        var options = new ChatCompletionOptions();
        var tools = _toolRegistry.GetAvailableTools("SUPER_ADMIN", activeApps).ToList();
        
        var formToolDefinition = new AgentToolDefinition(
            nameof(RequestFormInputCommand),
            "Request a user interface form to collect missing parameters for a target command.",
            "low",
            typeof(RequestFormInputCommand),
            false,
            ChatTool.CreateFunctionTool(
                nameof(RequestFormInputCommand),
                "Request a user interface form to collect missing parameters for a target command.",
                BinaryData.FromString(@"{""type"":""object"",""properties"":{""targetToolName"":{""type"":""string""},""partialData"":{""type"":""object"",""description"":""A JSON object containing any fields you already know. Must be an object or omitted entirely.""}},""required"":[""targetToolName""]}")
            )
        );
        options.Tools.Add(formToolDefinition.ChatTool);

        if (tools.Any()) foreach (var tool in tools) options.Tools.Add(tool.ChatTool);
        return options;
    }
}
