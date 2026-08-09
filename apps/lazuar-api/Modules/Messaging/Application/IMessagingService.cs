namespace Modules.Messaging.Application;

public interface IMessagingService
{
    Task SendMessageAsync(string recipient, string text);
}
