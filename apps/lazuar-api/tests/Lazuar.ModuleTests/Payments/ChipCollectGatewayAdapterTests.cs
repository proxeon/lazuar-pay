using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Payments.Infrastructure.Gateways;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class ChipCollectGatewayAdapterTests
{
    [Test]
    public async Task IssueRefundAsync_PostsMinorUnitsToPurchaseRefund()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(http);

        var adapter = new ChipCollectGatewayAdapter(
            factory,
            new ConfigurationBuilder().Build(),
            NullLogger<ChipCollectGatewayAdapter>.Instance);

        var ok = await adapter.IssueRefundAsync("chip-key", "purch_99", 12.34m);

        ok.Should().BeTrue();
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().Should().Be("https://gate.chip-in.asia/api/v1/purchases/purch_99/refund/");
        handler.LastBody.Should().Contain("\"amount\"");
        handler.LastBody.Should().Contain("1234");
    }

    [Test]
    public void ExtractVaultIds_RootRecurringTokenAndClientId()
    {
        using var doc = JsonDocument.Parse("""
            {
              "id": "purchase_abc",
              "is_recurring_token": true,
              "client": { "id": "client_123", "email": "a@b.com" }
            }
            """);

        var (customerId, tokenId) = ChipCollectGatewayAdapter.ExtractVaultIds(doc.RootElement);

        customerId.Should().Be("client_123");
        tokenId.Should().Be("purchase_abc");
    }

    [Test]
    public void ExtractVaultIds_PurchaseNodeTokenAndClient_FallsBackCustomerToToken()
    {
        using var doc = JsonDocument.Parse("""
            {
              "id": "purchase_nested",
              "purchase": {
                "is_recurring_token": true,
                "recurring_token": "tok_from_purchase"
              }
            }
            """);

        var (customerId, tokenId) = ChipCollectGatewayAdapter.ExtractVaultIds(doc.RootElement);

        tokenId.Should().Be("tok_from_purchase");
        customerId.Should().Be("tok_from_purchase");
    }

    [Test]
    public void ExtractVaultIds_NoRecurring_ReturnsNulls()
    {
        using var doc = JsonDocument.Parse("""
            { "id": "purchase_once", "client": { "id": "client_x" } }
            """);

        var (customerId, tokenId) = ChipCollectGatewayAdapter.ExtractVaultIds(doc.RootElement);

        tokenId.Should().BeNull();
        customerId.Should().Be("client_x");
    }

    [Test]
    public async Task ParseWebhook_MissingSignature_IsNotVerified()
    {
        var (adapter, _) = CreateSignedAdapter();

        var result = await adapter.ParseWebhookAsync(
            "unused",
            "-----BEGIN PUBLIC KEY-----\nMIIB\n-----END PUBLIC KEY-----",
            """{"event_type":"purchase.paid","id":"purch_1"}""",
            new Dictionary<string, string>());

        result.Verified.Should().BeFalse();
        result.Error.Should().Contain("X-Signature");
    }

    [Test]
    public async Task ParseWebhook_BadSignature_IsNotVerified()
    {
        var (adapter, publicPem) = CreateSignedAdapter();
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Signature"] = Convert.ToBase64String(new byte[64])
        };

        var result = await adapter.ParseWebhookAsync(
            "unused",
            publicPem,
            """{"event_type":"purchase.paid","id":"purch_1"}""",
            headers);

        result.Verified.Should().BeFalse();
    }

    [Test]
    public async Task ParseWebhook_PurchasePaid_UsesRootId()
    {
        var body = """
            {
              "id": "purch_root_1",
              "event_type": "purchase.paid",
              "purchase": { "total": 5000, "currency": "MYR" }
            }
            """;
        var (result, _) = await ParseSignedAsync(body);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_COMPLETED");
        result.EventId.Should().Be("purch_root_1");
        result.GatewayTransactionId.Should().Be("purch_root_1");
        Guid.TryParse(result.EventId, out _).Should().BeFalse();
    }

    [Test]
    public async Task ParseWebhook_PurchasePaid_PrefersNestedPurchaseId()
    {
        var body = """
            {
              "id": "purch_root_ignored",
              "event_type": "purchase.paid",
              "purchase": { "id": "purch_nested_9", "total": 2500, "currency": "MYR" }
            }
            """;
        var (result, _) = await ParseSignedAsync(body);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_COMPLETED");
        result.EventId.Should().Be("purch_nested_9");
        result.GatewayTransactionId.Should().Be("purch_nested_9");
    }

    [Test]
    public async Task ParseWebhook_PurchasePaid_NoIds_IsNotVerified()
    {
        var body = """
            {
              "event_type": "purchase.paid",
              "purchase": { "total": 1000, "currency": "MYR" }
            }
            """;
        var (result, _) = await ParseSignedAsync(body);

        result.Verified.Should().BeFalse();
        result.Error.Should().Contain("purchase id");
        result.EventId.Should().BeEmpty();
    }

    [Test]
    public async Task ParseWebhook_Preauthorized_IsVerified_NotPaymentCompleted()
    {
        var body = """
            {
              "id": "purch_hold",
              "event_type": "purchase.preauthorized",
              "purchase": { "id": "purch_hold", "total": 1000 }
            }
            """;
        var (result, _) = await ParseSignedAsync(body);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("purchase.preauthorized");
        result.EventType.Should().NotBe("PAYMENT_COMPLETED");
    }

    [Test]
    public async Task ParseWebhook_PaymentFailure_UsesStablePurchaseId()
    {
        var body = """
            {
              "event_type": "purchase.payment_failure",
              "purchase": { "id": "purch_fail_1", "total": 1000, "currency": "MYR" }
            }
            """;
        var (result, _) = await ParseSignedAsync(body);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_FAILED");
        result.EventId.Should().Be("purch_fail_1");
        result.GatewayTransactionId.Should().Be("purch_fail_1");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        }
    }

    private static ChipCollectGatewayAdapter CreateAdapter() =>
        new(
            Substitute.For<IHttpClientFactory>(),
            new ConfigurationBuilder().Build(),
            NullLogger<ChipCollectGatewayAdapter>.Instance);

    private static (ChipCollectGatewayAdapter Adapter, string PublicPem) CreateSignedAdapter()
    {
        return (CreateAdapter(), ExportTestPublicPem());
    }

    private static async Task<(Modules.Payments.Application.Ports.GatewayWebhookParsedResult Result, string PublicPem)> ParseSignedAsync(string body)
    {
        using var rsa = RSA.Create(2048);
        var publicPem = ToSubjectPublicKeyPem(rsa);
        var signature = Convert.ToBase64String(
            rsa.SignData(Encoding.UTF8.GetBytes(body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Signature"] = signature
        };

        var result = await CreateAdapter().ParseWebhookAsync("unused", publicPem, body, headers);
        return (result, publicPem);
    }

    private static string ExportTestPublicPem()
    {
        using var rsa = RSA.Create(2048);
        return ToSubjectPublicKeyPem(rsa);
    }

    private static string ToSubjectPublicKeyPem(RSA rsa)
    {
        var der = rsa.ExportSubjectPublicKeyInfo();
        var b64 = Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks);
        return $"-----BEGIN PUBLIC KEY-----\n{b64}\n-----END PUBLIC KEY-----";
    }
}
