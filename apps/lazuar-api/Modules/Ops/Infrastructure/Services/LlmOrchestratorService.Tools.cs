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
        catch (JsonException ex)
        {
            payload = new { _error = $"System Error: Tool {definition.Name} rejected your payload. The JSON structure was invalid. Missing/Malformed field: {ex.Message}. Please output correct JSON and try again.", _raw_output = arguments };
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
                jsonNode = JsonNode.Parse(jsonObject.GetRawText()) ?? new JsonObject();
            }
            catch (JsonException ex)
            {
                return $"System Error: Tool {definition.Name} rejected your payload. The JSON structure was invalid. Error detail: {ex.Message}. Please output correct JSON and try again.";
            }

            jsonNode["OrganizationId"] = tenantId.ToString();

            var deserializeOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            
            object? args = null;
            try
            {
                args = jsonNode.Deserialize(definition.RequestType, deserializeOptions);
            }
            catch (JsonException ex)
            {
                return $"System Error: Tool {definition.Name} rejected your payload. The JSON structure was invalid. Error detail: {ex.Message}. Please output correct JSON and try again.";
            }

            if (args == null)
            {
                return $"System Error: Tool {definition.Name} failed to deserialize arguments.";
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
