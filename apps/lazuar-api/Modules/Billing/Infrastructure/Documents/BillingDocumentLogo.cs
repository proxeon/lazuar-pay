using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.Billing.Infrastructure.Documents;

internal static class BillingDocumentLogo
{
    public static async Task<byte[]?> TryFetchAsync(
        IHttpClientFactory httpClientFactory,
        string? logoUrl,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return null;

        try
        {
            var client = httpClientFactory.CreateClient();
            return await client.GetByteArrayAsync(logoUrl, ct);
        }
        catch
        {
            return null;
        }
    }
}
