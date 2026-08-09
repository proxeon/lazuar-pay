namespace Modules.Messaging.Infrastructure.Configuration;

public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    public string ApiKey { get; init; } = "";
    public string SenderEmail { get; init; } = "";
}
