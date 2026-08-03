using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Lazuar.Lhdn.Sdk.Generated;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Lazuar.Lhdn.Sdk;

/// <summary>
/// Custom HTTP handler to inject idempotency keys automatically for POST requests.
/// </summary>
internal class IdempotencyHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post && !request.Headers.Contains("Idempotency-Key"))
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

public static class LhdnClientFactory
{
    public static LhdnClient Create(string apiKey, string? baseUrl = null)
    {
        // Always send Authorization: Bearer sk_* so raw keys and pre-prefixed values both work.
        var authorizationValue = apiKey.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? apiKey
            : $"Bearer {apiKey}";

        var authProvider = new ApiKeyAuthenticationProvider(
            authorizationValue,
            "Authorization",
            ApiKeyAuthenticationProvider.KeyLocation.Header);

        // KiotaClientFactory.Create natively configures RetryHandler (Exponential Backoff)
        var handlers = KiotaClientFactory.CreateDefaultHandlers();
        handlers.Insert(0, new IdempotencyHandler()); 
        
        var httpClient = KiotaClientFactory.Create(handlers);

        var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient);
        
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            adapter.BaseUrl = baseUrl;
        }

        return new LhdnClient(adapter);
    }
}
