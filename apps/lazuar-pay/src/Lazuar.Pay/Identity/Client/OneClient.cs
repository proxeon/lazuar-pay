using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Lazuar.Pay.Identity.Client;

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
    readonly OneWhoamiCache? _cache;

    public OneClient(HttpClient http, IOptions<OneOptions> options, OneWhoamiCache? cache = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _cache = cache;
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
        var machine = Bearer.IsMachineKey(authorization);
        if (machine && _cache is not null && _cache.TryGet(authorization, out var cached))
        {
            return new OneCallResult<WhoamiResponse> { Value = cached, StatusCode = 200 };
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "me");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        if (!string.IsNullOrWhiteSpace(tenantHint))
        {
            request.Headers.TryAddWithoutValidation("X-Lazuar-Tenant-Id", tenantHint);
        }

        var result = await SendAsync(request, async (response, ct) =>
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

        if (result.StatusCode == 401)
        {
            _cache?.RemoveToken(authorization);
        }

        if (machine && result.StatusCode == 200 && result.Value is not null)
        {
            _cache?.Set(authorization, result.Value, machineKey: true);
        }

        return result;
    }

    internal async Task<OneCallResult<bool>> CheckMemberAsync(
        string authorization,
        string orgId,
        string? tenantHint,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"tenants/{Uri.EscapeDataString(orgId)}/authz/check");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        if (!string.IsNullOrWhiteSpace(tenantHint))
        {
            request.Headers.TryAddWithoutValidation("X-Lazuar-Tenant-Id", tenantHint);
        }

        request.Content = JsonContent.Create(
            new OneAuthzCheckRequest
            {
                Relation = "member",
                Object = new OneAuthzObject { Type = "tenant", Id = orgId }
            },
            options: Json);

        return await SendAsync(request, async (response, ct) =>
        {
            OneAuthzCheckResponse? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<OneAuthzCheckResponse>(Json, ct);
            }
            catch (JsonException)
            {
                return new OneCallResult<bool> { StatusCode = 503 };
            }

            return new OneCallResult<bool>
            {
                Value = body?.Allowed == true,
                StatusCode = 200
            };
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

            return new OneCallResult<T>
            {
                StatusCode = code,
                Detail = await ReadDetailAsync(response, cancellationToken)
            };
        }
    }

    static async Task<string?> ReadDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Truncate(text);
            }

            if (TryString(doc.RootElement, "detail", out var detail)
                || TryString(doc.RootElement, "title", out detail)
                || TryString(doc.RootElement, "message", out detail))
            {
                return detail;
            }
        }
        catch (JsonException)
        {
        }

        return Truncate(text);
    }

    static bool TryString(JsonElement root, string name, out string value)
    {
        value = "";
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var raw = el.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        value = raw.Trim();
        return true;
    }

    static string Truncate(string text) =>
        text.Length <= 200 ? text : text[..200];
}
