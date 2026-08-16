using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Payments.Infrastructure.Gateways;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class XenditGatewayAdapterTests
{
    private static XenditGatewayAdapter CreateAdapter() =>
        new(Substitute.For<System.Net.Http.IHttpClientFactory>(), NullLogger<XenditGatewayAdapter>.Instance);

    [Test]
    public async Task ParseWebhook_MissingToken_IsNotVerified()
    {
        var result = await CreateAdapter().ParseWebhookAsync(
            "xnd_secret",
            "callback-secret",
            """{"id":"inv_1","status":"PAID","amount":10,"currency":"MYR"}""",
            new Dictionary<string, string>());

        result.Verified.Should().BeFalse();
        result.Error.Should().Contain("x-callback-token");
    }

    [Test]
    public async Task ParseWebhook_Paid_MapsCompleted()
    {
        var body = """
            {
              "id": "inv_paid_1",
              "status": "PAID",
              "amount": 50,
              "paid_amount": 50,
              "currency": "MYR",
              "metadata": { "subscription_id": "sub-1", "tenant_id": "t1" }
            }
            """;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-callback-token"] = "callback-secret"
        };

        var result = await CreateAdapter().ParseWebhookAsync("xnd_secret", "callback-secret", body, headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_COMPLETED");
        result.GatewayTransactionId.Should().Be("inv_paid_1");
        result.AmountPaid.Should().Be(50m);
        result.Currency.Should().Be("MYR");
        result.Metadata.Should().ContainKey("subscription_id");
    }

    [Test]
    public async Task ParseWebhook_Expired_MapsFailed()
    {
        var body = """{"id":"inv_exp","status":"EXPIRED","amount":12,"currency":"IDR"}""";
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Callback-Token"] = "callback-secret"
        };

        var result = await CreateAdapter().ParseWebhookAsync("xnd_secret", "callback-secret", body, headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_FAILED");
        result.Currency.Should().Be("IDR");
    }

    [Test]
    public async Task ParseWebhook_PaidWithoutCurrency_DoesNotInventMyr()
    {
        var body = """{"id":"inv_noccy","status":"PAID","amount":10}""";
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-callback-token"] = "callback-secret"
        };

        var result = await CreateAdapter().ParseWebhookAsync("xnd_secret", "callback-secret", body, headers);

        result.Verified.Should().BeFalse();
        result.Error.Should().Contain("currency");
    }

    [Test]
    public void BuildInvoicePayload_FiltersUnknownChannels()
    {
        var meta = new Dictionary<string, string>
        {
            ["xendit_payment_methods"] = "GRABPAY,FAKEWALLET,SHOPEEPAY"
        };

        var payload = XenditGatewayAdapter.BuildInvoicePayload(
            Guid.CreateVersion7(),
            10m,
            "MYR",
            "Plan",
            "buyer@example.com",
            "https://ok",
            "https://no",
            meta,
            1);

        payload.Should().ContainKey("payment_methods");
        var methods = (List<string>)payload["payment_methods"];
        methods.Should().BeEquivalentTo(["GRABPAY", "SHOPEEPAY"]);
    }

    [Test]
    public void ChargeOffSession_AlwaysFalse_UntilTokenSoak()
    {
        CreateAdapter().ChargeOffSessionAsync(
            "k", "cus", "tok", 10m, "MYR", "d", "rcpt", Guid.CreateVersion7())
            .GetAwaiter().GetResult()
            .Should().BeFalse();
    }
}
