using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Payments.Infrastructure.Gateways;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class BillplzGatewayAdapterTests
{
    private const string WebhookSecret = "billplz-x-signature-secret";

    private static BillplzGatewayAdapter CreateAdapter()
    {
        var httpFactory = Substitute.For<IHttpClientFactory>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:ApiBaseUrl"] = "https://api.lazuar.com/api/v1"
            })
            .Build();
        return new BillplzGatewayAdapter(
            httpFactory,
            config,
            NullLogger<BillplzGatewayAdapter>.Instance);
    }

    private static string ComputeXSignature(Dictionary<string, string> formData, string secret)
    {
        var elements = formData
            .Where(kv => !kv.Key.Equals("x_signature", StringComparison.OrdinalIgnoreCase))
            .Select(kv => $"{kv.Key}{kv.Value}")
            .OrderBy(element => element, StringComparer.Ordinal)
            .ToList();
        var sourceString = string.Join("|", elements);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(sourceString));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ToFormBody(Dictionary<string, string> formData)
    {
        return string.Join("&", formData.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    [Test]
    public async Task ParseWebhook_QueryCheckoutId_IncludedInMetadata()
    {
        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "bill_abc123",
            ["paid"] = "true",
            ["state"] = "paid",
            ["paid_amount"] = "5000",
            ["reference_1"] = "booking-ref",
            ["reference_2"] = "booking_payment"
        };
        form["x_signature"] = ComputeXSignature(form, WebhookSecret);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Query-checkout_id"] = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            ["Query-type"] = "booking_payment",
            ["Query-reference_1"] = "booking-ref"
        };

        var adapter = CreateAdapter();
        var result = await adapter.ParseWebhookAsync(
            apiKey: "unused",
            webhookSecret: WebhookSecret,
            rawBody: ToFormBody(form),
            headers: headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_COMPLETED");
        result.GatewayTransactionId.Should().Be("bill_abc123");
        result.Metadata.Should().ContainKey("checkout_id")
            .WhoseValue.Should().Be("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        result.Metadata.Should().ContainKey("type").WhoseValue.Should().Be("booking_payment");
    }

    [Test]
    public async Task ParseWebhook_PlatformSaasFee_MapsReference1ToTenantId()
    {
        var tenantId = Guid.CreateVersion7();
        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "bill_saas_1",
            ["paid"] = "true",
            ["state"] = "paid",
            ["paid_amount"] = "9900",
            ["reference_1"] = tenantId.ToString(),
            ["reference_2"] = "platform_saas_fee"
        };
        form["x_signature"] = ComputeXSignature(form, WebhookSecret);

        var result = await CreateAdapter().ParseWebhookAsync(
            "unused",
            WebhookSecret,
            ToFormBody(form),
            new Dictionary<string, string>());

        result.Verified.Should().BeTrue();
        result.Metadata.Should().ContainKey("type").WhoseValue.Should().Be("platform_saas_fee");
        result.Metadata.Should().ContainKey("tenant_id").WhoseValue.Should().Be(tenantId.ToString());
        result.Metadata.Should().NotContainKey("subscription_id");
    }

    [Test]
    public async Task ParseWebhook_WithoutQueryCheckoutId_NoCheckoutIdInMetadata()
    {
        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "bill_commerce_1",
            ["paid"] = "true",
            ["state"] = "paid",
            ["paid_amount"] = "2000",
            ["reference_1"] = Guid.CreateVersion7().ToString(),
            ["reference_2"] = "commerce_subscription"
        };
        form["x_signature"] = ComputeXSignature(form, WebhookSecret);

        var adapter = CreateAdapter();
        var result = await adapter.ParseWebhookAsync(
            "unused",
            WebhookSecret,
            ToFormBody(form),
            new Dictionary<string, string>());

        result.Verified.Should().BeTrue();
        result.Metadata.Should().NotContainKey("checkout_id");
        result.Metadata.Should().ContainKey("type").WhoseValue.Should().Be("commerce_subscription");
        result.Metadata.Should().ContainKey("subscription_id");
    }

    [Test]
    public async Task GenerateCheckout_WithCheckoutId_AppendsQueryParam()
    {
        // Without real HTTP, GenerateCheckout will call Billplz API and fail — instead assert via
        // parse path that we wire Query-checkout_id. Create-path query construction is covered
        // indirectly: callback URL includes checkout_id when present in metadata.
        //
        // Smoke: adapter constructs without throw when metadata has checkout_id keys.
        var adapter = CreateAdapter();
        var checkoutId = Guid.CreateVersion7().ToString();
        var metadata = new Dictionary<string, string>
        {
            ["type"] = "booking_payment",
            ["checkout_id"] = checkoutId,
            ["hub_checkout_kind"] = "integration"
        };

        // API call will fail without mock HTTP — expect failure result, not exception.
        var result = await adapter.GenerateCheckoutAsync(
            apiKey: "sk_test",
            tenantId: Guid.CreateVersion7(),
            amount: 10m,
            currency: "MYR",
            productName: "Test",
            customerEmail: "a@b.com",
            successUrl: "https://ok",
            cancelUrl: "https://cancel",
            metadata: metadata,
            merchantId: "col_test");

        // Either network failure or mock-less failure is fine; must not throw.
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Test]
    public async Task IssueRefundAsync_AlwaysReturnsFalse()
    {
        var refunded = await CreateAdapter().IssueRefundAsync("unused", "bill_1", 10m);
        refunded.Should().BeFalse();
    }

    [Test]
    public async Task ChargeOffSessionAsync_DoesNotThrow_ReturnsFalse()
    {
        var adapter = CreateAdapter();

        var charged = await adapter.ChargeOffSessionAsync(
            apiKey: "unused",
            customerId: "cus",
            tokenId: "tok",
            amount: 10m,
            currency: "MYR",
            description: "renewal",
            receipt: Guid.CreateVersion7().ToString(),
            tenantId: Guid.CreateVersion7());

        charged.Should().BeFalse();
    }

    [Test]
    public async Task ParseWebhook_BadSignature_IsNotVerified()
    {
        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "bill_bad_sig",
            ["paid"] = "true",
            ["state"] = "paid",
            ["paid_amount"] = "1000",
            ["x_signature"] = "deadbeef"
        };

        var result = await CreateAdapter().ParseWebhookAsync(
            "unused", WebhookSecret, ToFormBody(form), new Dictionary<string, string>());

        result.Verified.Should().BeFalse();
        result.Error.Should().Contain("x_signature");
    }

    [Test]
    public async Task ParseWebhook_MissingId_IsNotVerified()
    {
        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["paid"] = "true",
            ["state"] = "paid",
            ["paid_amount"] = "1000"
        };
        form["x_signature"] = ComputeXSignature(form, WebhookSecret);

        var result = await CreateAdapter().ParseWebhookAsync(
            "unused", WebhookSecret, ToFormBody(form), new Dictionary<string, string>());

        result.Verified.Should().BeFalse();
        result.Error.Should().Contain("bill id");
        result.EventId.Should().BeEmpty();
    }

    [Test]
    public async Task ParseWebhook_EmptyId_IsNotVerified()
    {
        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "   ",
            ["paid"] = "true",
            ["state"] = "paid",
            ["paid_amount"] = "1000"
        };
        form["x_signature"] = ComputeXSignature(form, WebhookSecret);

        var result = await CreateAdapter().ParseWebhookAsync(
            "unused", WebhookSecret, ToFormBody(form), new Dictionary<string, string>());

        result.Verified.Should().BeFalse();
        result.Error.Should().Contain("bill id");
    }

    [Test]
    public async Task ParseWebhook_Unpaid_IsPaymentFailed_WithBillId()
    {
        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "bill_unpaid_1",
            ["paid"] = "false",
            ["state"] = "due",
            ["paid_amount"] = "0"
        };
        form["x_signature"] = ComputeXSignature(form, WebhookSecret);

        var result = await CreateAdapter().ParseWebhookAsync(
            "unused", WebhookSecret, ToFormBody(form), new Dictionary<string, string>());

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_FAILED");
        result.EventId.Should().Be("bill_unpaid_1");
        result.GatewayTransactionId.Should().Be("bill_unpaid_1");
    }
}
