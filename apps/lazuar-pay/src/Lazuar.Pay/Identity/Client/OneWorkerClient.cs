using Microsoft.Extensions.Options;

namespace Lazuar.Pay.Identity.Client;

/// <summary>
/// Process-bound One client for hosted jobs. Not the interactive <see cref="OneClient"/>.
/// <c>One:ApiKey</c> must be a One <c>lzr_sk_</c> for <c>One:WorkerOrgId</c> only.
/// Stripe/Hub <c>sk_</c> is refused. Interactive doors still require a request Bearer.
/// </summary>
public sealed class OneWorkerClient
{
    readonly HttpClient _http;

    public OneWorkerClient(HttpClient http, IOptions<OneOptions> options)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        var opt = options.Value;
        WorkerOrgId = string.IsNullOrWhiteSpace(opt.WorkerOrgId) ? null : opt.WorkerOrgId.Trim();
        var baseUrl = (string.IsNullOrWhiteSpace(opt.BaseUrl)
            ? "http://localhost:8080/api/v1"
            : opt.BaseUrl).TrimEnd('/') + "/";
        _http.BaseAddress = new Uri(baseUrl);
        var timeout = opt.TimeoutSeconds <= 0 ? 5 : opt.TimeoutSeconds;
        _http.Timeout = TimeSpan.FromSeconds(timeout);

        var key = opt.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        ThrowIfInvalidKey(key);
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + key);
        if (!string.IsNullOrWhiteSpace(WorkerOrgId))
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Lazuar-Tenant-Id", WorkerOrgId);
        }
    }

    internal HttpClient Http => _http;

    public string? WorkerOrgId { get; }

    public static void ThrowIfInvalid(IConfiguration config)
    {
        var key = config["One:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        ThrowIfInvalidKey(key);
        var org = config["One:WorkerOrgId"]?.Trim();
        if (string.IsNullOrWhiteSpace(org))
        {
            throw new InvalidOperationException("One:WorkerOrgId is required when One:ApiKey is set");
        }
    }

    public static void ThrowIfInvalidKey(string key)
    {
        if (key.StartsWith("sk_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("One:ApiKey must be a One lzr_sk_ key, not sk_");
        }

        if (!key.StartsWith("lzr_sk_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("One:ApiKey must start with lzr_sk_");
        }
    }
}
