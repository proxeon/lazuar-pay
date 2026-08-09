using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Modules.Lhdn.Application.Ports;

namespace Modules.Lhdn.Infrastructure.Gateways;

/// <summary>
/// MyInvois HTTP gateway: token, submit, status poll, TIN validation, cancel.
/// Logic is split across partials by operation for navigability.
/// </summary>
public partial class LhdnGatewayAdapter : ILhdnGatewayAdapter
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LhdnGatewayAdapter> _logger;

    private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _loginLimiters = new();
    private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _submitLimiters = new();
    private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _pollLimiters = new();
    private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _tinLimiters = new();
    private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _cancelLimiters = new();

    public LhdnGatewayAdapter(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<LhdnGatewayAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    private string GetBaseUrl()
    {
        return _configuration["Lhdn:BaseUrl"]?.TrimEnd('/') ?? "https://preprod-api.myinvois.hasil.gov.my";
    }

    private async Task EnforceRateLimitAsync(ConcurrentDictionary<string, TokenBucketRateLimiter> registry, string clientId, int limit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientId)) return;

        var limiter = registry.GetOrAdd(clientId, _ => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = limit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            TokensPerPeriod = limit,
            AutoReplenishment = true
        }));

        await limiter.AcquireAsync(1, ct);
    }

    private void TryAddIntermediaryHeader(HttpRequestMessage request, bool isIntermediary, string? tenantTin)
    {
        if (isIntermediary && !string.IsNullOrWhiteSpace(tenantTin))
        {
            request.Headers.Add("onbehalfof", tenantTin.Trim());
        }
    }

    private static int? ExtractRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta.HasValue == true)
        {
            return (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
        }

        if (response.Headers.TryGetValues("x-rate-limit-reset", out var resetValues) &&
            long.TryParse(resetValues.FirstOrDefault(), out var resetEpoch))
        {
            var resetTime = DateTimeOffset.FromUnixTimeSeconds(resetEpoch).UtcDateTime;
            var delay = (int)(resetTime - DateTime.UtcNow).TotalSeconds;
            return delay > 0 ? delay : 60;
        }

        return 60;
    }
}
