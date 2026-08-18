namespace Modules.One.Application;

/// <summary>
/// Same-process eviction for API-key principal cache. Revoke must not wait for outbox.
/// </summary>
public interface IApiKeyAuthCache
{
    void Evict(string keyHash);
}
