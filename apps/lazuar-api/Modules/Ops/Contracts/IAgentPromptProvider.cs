namespace Modules.Ops.Contracts;

/// <summary>
/// Per-app system-prompt rules injected into the Ops agent orchestrator when the app is active.
/// Cross-module extension point (e.g. Billing implements this).
/// </summary>
public interface IAgentPromptProvider
{
    string GetAppId();
    string GetSystemPromptRules();
}
