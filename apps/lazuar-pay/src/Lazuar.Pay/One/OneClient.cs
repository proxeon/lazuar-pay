using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Lazuar.Pay.One;

/// <summary>
/// Typed client to One. Tests replace this type (or its HttpMessageHandler) in ConfigureTestServices.
/// </summary>
public sealed class OneClient
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

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

    internal async Task<OneCallResult<WhoamiResponse>> GetWhoamiAsync(
        string authorization,
        string? tenantHint,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "me");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        if (!string.IsNullOrWhiteSpace(tenantHint))
        {
            request.Headers.TryAddWithoutValidation("X-Lazuar-Tenant-Id", tenantHint);
        }

        return await SendAsync(request, async (response, ct) =>
        {
            OneMeResponse? me;
            try
            {
                me = await response.Content.ReadFromJsonAsync<OneMeResponse>(Json, ct);
            }
            catch (JsonException)
            {
                return new OneCallResult<WhoamiResponse> { StatusCode = 503 };
            }

            var who = OneMeMapper.ToWhoami(me);
            if (who is null)
            {
                return new OneCallResult<WhoamiResponse> { StatusCode = 503 };
            }

            return new OneCallResult<WhoamiResponse> { Value = who, StatusCode = 200 };
        }, cancellationToken);
    }

    private async Task<OneCallResult<T>> SendAsync<T>(
        HttpRequestMessage request,
        Func<HttpResponseMessage, CancellationToken, Task<OneCallResult<T>>> onOk,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return new OneCallResult<T> { TimedOut = true };
        }
        catch (HttpRequestException)
        {
            return new OneCallResult<T> { TransportFailed = true };
        }

        using (response)
        {
            var code = (int)response.StatusCode;
            if (code == 200)
            {
                return await onOk(response, cancellationToken);
            }

            return new OneCallResult<T> { StatusCode = code };
        }
    }
}
