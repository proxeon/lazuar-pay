namespace Modules.Messaging.Application;

public interface IMessagingService
{
    /// <summary>True only when a paid provider actually sends. Console stub is not billable.</summary>
    bool IsBillable { get; }

    Task SendMessageAsync(string recipient, string text);
}
