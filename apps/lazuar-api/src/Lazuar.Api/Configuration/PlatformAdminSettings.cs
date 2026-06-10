namespace Lazuar.Api.Configuration;

/// <summary>
/// Maps the root platform administrator credentials from environment variables.
/// These are utilized during system startup to bootstrap the genesis accounts.
/// </summary>
public sealed class PlatformAdminSettings
{
    public string Emails { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
