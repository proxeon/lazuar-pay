using System;

namespace Modules.Commerce.Domain;

/// <summary>
/// Shared step shape for live <c>DunningStep</c> rows and a frozen campaign snapshot.
/// </summary>
public interface IDunningStepCopy
{
    Guid Id { get; }
    int DayOffset { get; }
    string ActionType { get; }
    string? Subject { get; }
    string? EmailBody { get; }
    string? WhatsAppBody { get; }
}
