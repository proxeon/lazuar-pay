using Microsoft.Extensions.Options;

namespace Lazuar.Pay.One;

/// <summary>
/// Typed client to One. Tests replace this type (or its HttpMessageHandler) in ConfigureTestServices.
/// </summary>
public sealed class OneClient
{
    private readonly HttpClient _http;

    public OneClient(HttpClient http, IOptions<OneOptions> options)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        var opt = options.Value;
        var baseUrl = (string.IsNullOrWhiteSpace(opt.BaseUrl)
            ? "http://localhost:8080/api/v1"
            : opt.BaseUrl).TrimEnd('/') + "/";
        _http.BaseAddress = new Uri(baseUrl);
        var timeout = opt.TimeoutSeconds <= 0 ? 5 : opt.TimeoutSeconds;
        _http.Timeout = TimeSpan.FromSeconds(timeout);
    }

    internal HttpClient Http => _http;
}
