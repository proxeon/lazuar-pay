namespace Lazuar.Pay.Rails;

public readonly record struct HostedSession(string RedirectUrl, string? ProviderSessionId);
