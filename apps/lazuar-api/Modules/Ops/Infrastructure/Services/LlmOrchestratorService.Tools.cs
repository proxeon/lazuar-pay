// apps/lazuar-api/Modules/Ops/Infrastructure/Services/LlmOrchestratorService.Tools.cs
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Lazuar.ApiTypes;
using Modules.Ops.Application.Services;

namespace Modules.Ops.Infrastructure.Services;

public partial class LlmOrchestratorService
{
    private ProposedActionDto BuildProposedAction(AgentToolDefinition definition, string arguments)
    {
        object payload;
        try
        {
            var cleanArgs = string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments;
            payload = JsonSerializer.Deserialize<object>(cleanArgs) ?? new object();
        }
        catch (JsonException)
        {
            payload = new { _error = "The AI generated invalid parameters.", _raw_output = arguments };
        }

        var name = definition.Name.Replace("Command", "");
        var intent = string.Concat(name.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart();

        return new ProposedActionDto
        {
            Idempotency_key = Guid.CreateVersion7().ToString(),
            Tool_name = definition.Name,
            Intent_title = intent,
            Severity = definition.Severity,
            Human_readable_summary = $"Proposing to execute {intent}.",
            Command_payload = payload
        };
    }

    private async Task<string> ExecuteReadToolAsync(AgentToolDefinition definition, string arguments, Guid tenantId, CancellationToken ct)
    {
        try
        {
            var cleanArgs = string.IsNullOrWhiteSpace(arguments) || arguments.Trim() == "{}"
                ? "{}"
                : arguments;

            JsonNode jsonNode;
            try
            {
                var jsonObject = JsonSerializer.Deserialize<JsonElement>(cleanArgs);
                jsonNode = JsonNode.Parse(jsonObject.GetRawText()) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                jsonNode = new JsonObject();
            }

            jsonNode["OrganizationId"] = tenantId.ToString();

            var deserializeOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var args = jsonNode.Deserialize(definition.RequestType, deserializeOptions);

            if (args == null)
            {
                return "Error: Failed to deserialize arguments into command.";
            }

            var result = await _mediator.Send(args, ct);
            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
