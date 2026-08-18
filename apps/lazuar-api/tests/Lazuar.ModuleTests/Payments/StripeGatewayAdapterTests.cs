using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Payments.Infrastructure.Gateways;
using NUnit.Framework;
using Stripe;
using Stripe.Checkout;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class StripeGatewayAdapterTests
{
    [Test]
    public void FormatRefundIdempotencyKey_UsesTransactionAndMinorAmount()
    {
        StripeGatewayAdapter.FormatRefundIdempotencyKey("pi_abc", 12.34m)
            .Should().Be("lazuar-refund:pi_abc:1234");
        StripeGatewayAdapter.IsRefundSucceeded("succeeded").Should().BeTrue();
        StripeGatewayAdapter.IsRefundSucceeded("pending").Should().BeFalse();
        StripeGatewayAdapter.IsRefundSucceeded("failed").Should().BeFalse();
    }

    [Test]
    public void ApplyCardWalletPaymentMethodTypes_SetsCardOnly()
    {
        var options = new SessionCreateOptions();

        StripeGatewayAdapter.ApplyCardWalletPaymentMethodTypes(options);

        options.PaymentMethodTypes.Should().Equal(StripeGatewayAdapter.CardPaymentMethodType);
        options.PaymentMethodTypes.Should().HaveCount(1);
        options.PaymentMethodTypes.Should().NotContain("apple_pay");
        options.PaymentMethodTypes.Should().NotContain("google_pay");
        options.PaymentMethodTypes.Should().NotContain("fpx");
    }

    [Test]
    public void ApplyCardWalletPaymentMethodTypes_DoesNotTouchPaymentIntentData()
    {
        var withoutPi = new SessionCreateOptions();
        StripeGatewayAdapter.ApplyCardWalletPaymentMethodTypes(withoutPi);
        withoutPi.PaymentIntentData.Should().BeNull();

        var metadata = new Dictionary<string, string> { ["checkout_id"] = "cs_meta" };
        var withPi = new SessionCreateOptions
        {
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = metadata
            }
        };

        StripeGatewayAdapter.ApplyCardWalletPaymentMethodTypes(withPi);
        StripeGatewayAdapter.ApplySetupFutureUsage(withPi, setupFutureUsage: true);

        withPi.PaymentMethodTypes.Should().Equal(StripeGatewayAdapter.CardPaymentMethodType);
        withPi.PaymentIntentData.Should().NotBeNull();
        withPi.PaymentIntentData!.Metadata.Should().BeSameAs(metadata);
        withPi.PaymentIntentData.SetupFutureUsage.Should().Be("off_session");
        withPi.CustomerCreation.Should().Be("always");
    }

    [Test]
    public void CreateCheckoutSessionOptions_IncludesCard_NotApplePay()
    {
        var metadata = new Dictionary<string, string> { ["checkout_id"] = "cs_lp037" };

        var options = StripeGatewayAdapter.CreateCheckoutSessionOptions(
            Guid.CreateVersion7(),
            25m,
            "MYR",
            "Widget",
            "buyer@example.com",
            "https://ok.example/success",
            "https://ok.example/cancel",
            metadata,
            setupFutureUsage: true);

        options.PaymentMethodTypes.Should().Contain(StripeGatewayAdapter.CardPaymentMethodType);
        options.PaymentMethodTypes.Should().Equal(StripeGatewayAdapter.CardPaymentMethodType);
        options.PaymentMethodTypes.Should().NotContain("apple_pay");
        options.PaymentMethodTypes.Should().NotContain("google_pay");
        options.PaymentIntentData.Should().NotBeNull();
        options.PaymentIntentData!.SetupFutureUsage.Should().Be("off_session");
    }

    [Test]
    public void NonStripeAdapters_DoNotSendApplePayOrPaymentMethodTypes()
    {
        var gatewaysDir = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "Modules", "Payments", "Infrastructure", "Gateways"));

        foreach (var file in new[]
        {
            "BillplzGatewayAdapter.cs",
            "ChipCollectGatewayAdapter.cs",
            "RazorpayGatewayAdapter.cs",
            "XenditGatewayAdapter.cs"
        })
        {
            var path = Path.Combine(gatewaysDir, file);
            System.IO.File.Exists(path).Should().BeTrue($"Missing adapter: {path}");

            var src = System.IO.File.ReadAllText(path);
            src.Should().NotContain("apple_pay");
            src.Should().NotContain("google_pay");
            src.Should().NotContain("PaymentMethodTypes");
            src.Should().NotContain("payment_method_types");
        }
    }

    [Test]
    public void ApplyPayingTenantMetadata_PreservesPayingTenant_AndStampsPlatformTenant()
    {
        var paying = Guid.CreateVersion7();
        var system = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var metadata = new Dictionary<string, string> { ["tenant_id"] = paying.ToString() };

        StripeGatewayAdapter.ApplyPayingTenantMetadata(metadata, system);

        metadata["tenant_id"].Should().Be(paying.ToString());
        metadata["platform_tenant_id"].Should().Be(system.ToString());
    }

    [Test]
    public void CreateCheckoutSessionOptions_HasNoApplicationFeeOrTransfer_AndKeepsPayingTenant()
    {
        var paying = Guid.CreateVersion7();
        var system = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var metadata = new Dictionary<string, string>
        {
            ["type"] = "platform_saas_fee",
            ["tenant_id"] = paying.ToString()
        };

        var options = StripeGatewayAdapter.CreateCheckoutSessionOptions(
            system,
            99m,
            "MYR",
            "Hub Starter (monthly)",
            "ada@example.com",
            "https://ok",
            "https://cancel",
            metadata);

        options.PaymentIntentData.Should().NotBeNull();
        options.PaymentIntentData!.ApplicationFeeAmount.Should().BeNull();
        options.PaymentIntentData.TransferData.Should().BeNull();
        options.Metadata!["tenant_id"].Should().Be(paying.ToString());
        options.Metadata["platform_tenant_id"].Should().Be(system.ToString());
        options.PaymentIntentData.Metadata!["tenant_id"].Should().Be(paying.ToString());
        options.PaymentIntentData.Metadata["platform_tenant_id"].Should().Be(system.ToString());
    }

    [Test]
    public void CreateCheckoutSessionOptions_TenantGmvCheckout_HasZeroPlatformFee_AndKeepsPayingTenant()
    {
        var tenant = Guid.CreateVersion7();
        var metadata = new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["tenant_id"] = tenant.ToString()
        };

        var options = StripeGatewayAdapter.CreateCheckoutSessionOptions(
            tenant,
            80m,
            "MYR",
            "Membership",
            "buyer@example.com",
            "https://ok",
            "https://cancel",
            metadata);

        options.PaymentIntentData.Should().NotBeNull();
        options.PaymentIntentData!.ApplicationFeeAmount.Should().BeNull();
        options.PaymentIntentData.TransferData.Should().BeNull();
        options.Metadata!["tenant_id"].Should().Be(tenant.ToString());
        options.PaymentIntentData.Metadata!["tenant_id"].Should().Be(tenant.ToString());
        options.Metadata.Should().NotContainKey("platform_tenant_id");
    }

    [Test]
    public void PaymentAdapters_DoNotSetConnectApplicationFeeOrTransfer()
    {
        var gatewaysDir = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "Modules", "Payments", "Infrastructure", "Gateways"));

        foreach (var file in new[]
        {
            "StripeGatewayAdapter.cs",
            "BillplzGatewayAdapter.cs",
            "ChipCollectGatewayAdapter.cs",
            "RazorpayGatewayAdapter.cs",
            "XenditGatewayAdapter.cs"
        })
        {
            var path = Path.Combine(gatewaysDir, file);
            System.IO.File.Exists(path).Should().BeTrue($"Missing adapter: {path}");

            var src = System.IO.File.ReadAllText(path);
            src.Should().NotContain("ApplicationFeeAmount");
            src.Should().NotContain("application_fee");
            src.Should().NotContain("TransferData");
            src.Should().NotContain("transfer_data");
        }
    }

    [Test]
    public void CreateCheckoutSessionOptions_ZeroAmountWithSetup_UsesSetupMode()
    {
        var metadata = new Dictionary<string, string> { ["type"] = "commerce_subscription" };

        var options = StripeGatewayAdapter.CreateCheckoutSessionOptions(
            Guid.CreateVersion7(),
            0m,
            "MYR",
            "Membership",
            "buyer@example.com",
            "https://ok.example/success",
            "https://ok.example/cancel",
            metadata,
            setupFutureUsage: true);

        options.Mode.Should().Be("setup");
        options.PaymentIntentData.Should().BeNull();
        options.LineItems.Should().BeNull();
        options.SetupIntentData.Should().NotBeNull();
        options.SetupIntentData!.Metadata.Should().BeSameAs(metadata);
        options.CustomerCreation.Should().Be("always");
        options.Currency.Should().Be("myr");
        options.PaymentMethodTypes.Should().Equal(StripeGatewayAdapter.CardPaymentMethodType);
    }

    [Test]
    public void ApplySetupFutureUsage_WhenTrue_SetsOffSessionAndAlwaysCreatesCustomer()
    {
        var options = new SessionCreateOptions
        {
            PaymentIntentData = new SessionPaymentIntentDataOptions()
        };

        StripeGatewayAdapter.ApplySetupFutureUsage(options, setupFutureUsage: true);

        options.PaymentIntentData!.SetupFutureUsage.Should().Be("off_session");
        options.CustomerCreation.Should().Be("always");
    }

    [Test]
    public void ApplySetupFutureUsage_WhenFalse_DoesNotSetCustomerCreation()
    {
        var options = new SessionCreateOptions
        {
            PaymentIntentData = new SessionPaymentIntentDataOptions()
        };

        StripeGatewayAdapter.ApplySetupFutureUsage(options, setupFutureUsage: false);

        options.PaymentIntentData!.SetupFutureUsage.Should().BeNull();
        options.CustomerCreation.Should().BeNull();
    }

    [Test]
    public void IsOffSessionSucceeded_OnlySucceededIsTrue()
    {
        StripeGatewayAdapter.IsOffSessionSucceeded("succeeded").Should().BeTrue();
        StripeGatewayAdapter.IsOffSessionSucceeded("processing").Should().BeFalse();
        StripeGatewayAdapter.IsOffSessionSucceeded("requires_action").Should().BeFalse();
        StripeGatewayAdapter.IsOffSessionSucceeded("failed").Should().BeFalse();
    }

    [Test]
    public void CreateOffSessionRequestOptions_WhenKeyPresent_SetsIdempotencyKey()
    {
        var eventId = Guid.CreateVersion7().ToString();

        var options = StripeGatewayAdapter.CreateOffSessionRequestOptions(eventId);

        options.Should().NotBeNull();
        options!.IdempotencyKey.Should().Be(eventId);
    }

    [Test]
    public void ResolveOffSessionIdempotencyKey_PrefersChargeAttemptId()
    {
        var attemptId = Guid.CreateVersion7();
        var fallback = Guid.CreateVersion7().ToString();

        var key = StripeGatewayAdapter.ResolveOffSessionIdempotencyKey(attemptId, fallback);

        key.Should().Be("lazuar-offsession:" + attemptId);
        StripeGatewayAdapter.FormatOffSessionIdempotencyKey(attemptId)
            .Should().Be(key);
    }

    [Test]
    public void ResolveOffSessionIdempotencyKey_FallsBackWhenAttemptMissing()
    {
        StripeGatewayAdapter.ResolveOffSessionIdempotencyKey(null, "evt_1")
            .Should().Be("evt_1");
        StripeGatewayAdapter.ResolveOffSessionIdempotencyKey(null, " ")
            .Should().BeNull();
    }

    [Test]
    public void BuildOffSessionMetadata_IncludesChargeAttemptId()
    {
        var tenantId = Guid.CreateVersion7();
        var campaignId = Guid.CreateVersion7();
        var attemptId = Guid.CreateVersion7();
        var receipt = Guid.CreateVersion7().ToString();

        var meta = StripeGatewayAdapter.BuildOffSessionMetadata(receipt, tenantId, campaignId, attemptId);

        meta["type"].Should().Be("commerce_subscription");
        meta["subscription_id"].Should().Be(receipt);
        meta["tenant_id"].Should().Be(tenantId.ToString());
        meta["dunning_campaign_id"].Should().Be(campaignId.ToString());
        meta["charge_attempt_id"].Should().Be(attemptId.ToString());
    }

    [Test]
    public void BuildOffSessionMetadata_IncludesSstWhenTaxAmountPositive()
    {
        var meta = StripeGatewayAdapter.BuildOffSessionMetadata(
            Guid.CreateVersion7().ToString(),
            Guid.CreateVersion7(),
            null,
            null,
            taxAmount: 8m,
            taxType: "02");

        meta["sst_tax_amount"].Should().Be("8.00");
        meta["sst_tax_type"].Should().Be("02");
    }

    [Test]
    public void CreateOffSessionRequestOptions_WhenMissing_ReturnsNull()
    {
        StripeGatewayAdapter.CreateOffSessionRequestOptions(null).Should().BeNull();
        StripeGatewayAdapter.CreateOffSessionRequestOptions(" ").Should().BeNull();
    }

    [Test]
    public void MapPaymentIntentPaymentFailed_UsesPiMetadataAndId()
    {
        var subscriptionId = Guid.CreateVersion7();
        var pi = new PaymentIntent
        {
            Id = "pi_failed_renew",
            Amount = 4990,
            Currency = "myr",
            CustomerId = "cus_1",
            PaymentMethodId = "pm_1",
            Metadata = new Dictionary<string, string>
            {
                ["type"] = "commerce_subscription",
                ["subscription_id"] = subscriptionId.ToString(),
                ["receipt"] = subscriptionId.ToString()
            }
        };

        var parsed = StripeGatewayAdapter.MapPaymentIntentPaymentFailed(pi, "evt_pi_failed");

        parsed.Verified.Should().BeTrue();
        parsed.EventType.Should().Be("PAYMENT_FAILED");
        parsed.EventId.Should().Be("evt_pi_failed");
        parsed.GatewayTransactionId.Should().Be("pi_failed_renew");
        parsed.AmountPaid.Should().Be(49.90m);
        parsed.Metadata["subscription_id"].Should().Be(subscriptionId.ToString());
        parsed.Metadata["receipt"].Should().Be(subscriptionId.ToString());
        parsed.GatewayCustomerId.Should().Be("cus_1");
        parsed.GatewayTokenId.Should().Be("pm_1");
    }

    [Test]
    public void ApplyBalanceTransactionFee_MissingCharge_IsUnknown()
    {
        decimal fee = 0;
        decimal fx = 1;
        var baseCurrency = "MYR";

        StripeGatewayAdapter.ApplyBalanceTransactionFee(null, ref fee, ref fx, ref baseCurrency).Should().BeFalse();
        fee.Should().Be(0m);

        var noBt = new Charge { Id = "ch_1" };
        StripeGatewayAdapter.ApplyBalanceTransactionFee(noBt, ref fee, ref fx, ref baseCurrency).Should().BeFalse();
        fee.Should().Be(0m);
    }

    [Test]
    public void ApplyBalanceTransactionFee_ExpandedBalanceTransaction_IsKnown()
    {
        decimal fee = 0;
        decimal fx = 1;
        var baseCurrency = "MYR";
        var charge = new Charge
        {
            BalanceTransaction = new BalanceTransaction
            {
                Fee = 123,
                Currency = "myr",
                ExchangeRate = 1.0m
            }
        };

        StripeGatewayAdapter.ApplyBalanceTransactionFee(charge, ref fee, ref fx, ref baseCurrency).Should().BeTrue();
        fee.Should().Be(1.23m);
        baseCurrency.Should().Be("MYR");
    }

    [Test]
    public void MapPaymentIntentPaymentFailed_CopiesDeclineCode()
    {
        var pi = new PaymentIntent
        {
            Id = "pi_stolen",
            Amount = 1000,
            Currency = "myr",
            LastPaymentError = new StripeError { DeclineCode = "stolen_card", Message = "Your card was stolen." }
        };

        var parsed = StripeGatewayAdapter.MapPaymentIntentPaymentFailed(pi, "evt_stolen");

        parsed.Metadata.Should().ContainKey("decline_code");
        parsed.Metadata["decline_code"].Should().Be("stolen_card");
        parsed.Error.Should().Contain("stolen");
    }

    private const string WebhookSecret = "whsec_test_lp090";
    private const string StripeApiVersion = "2025-03-31.basil";

    [Test]
    public async Task ParseWebhook_MissingStripeSignature_IsNotVerified()
    {
        var adapter = new StripeGatewayAdapter(NullLogger<StripeGatewayAdapter>.Instance);
        var result = await adapter.ParseWebhookAsync(
            "sk_test",
            WebhookSecret,
            SessionCompletedJson("evt_1", "cs_1", "pi_1"),
            new Dictionary<string, string>());

        result.Verified.Should().BeFalse();
        result.Error.Should().Contain("Stripe-Signature");
    }

    [Test]
    public async Task ParseWebhook_BadSecret_IsNotVerified()
    {
        var json = SessionCompletedJson("evt_bad", "cs_1", "pi_1");
        var adapter = new StripeGatewayAdapter(NullLogger<StripeGatewayAdapter>.Instance);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stripe-Signature"] = SignStripe(json, WebhookSecret)
        };

        var result = await adapter.ParseWebhookAsync("sk_test", "whsec_wrong", json, headers);

        result.Verified.Should().BeFalse();
    }

    [Test]
    public async Task ParseWebhook_CheckoutSessionCompleted_UsesEventIdAndPaymentIntent()
    {
        var json = SessionCompletedJson("evt_cs_1", "cs_test_1", "pi_test_1");
        var adapter = new StripeGatewayAdapter(NullLogger<StripeGatewayAdapter>.Instance);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stripe-Signature"] = SignStripe(json, WebhookSecret)
        };

        var result = await adapter.ParseWebhookAsync("sk_test", WebhookSecret, json, headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_COMPLETED");
        result.EventId.Should().Be("evt_cs_1");
        result.GatewayTransactionId.Should().Be("pi_test_1");
    }

    [Test]
    public async Task ParseWebhook_CheckoutSessionCompleted_SetupIntentWithoutPi_ExtractsCustomerAndPaymentMethod()
    {
        var json = SetupSessionCompletedJson("evt_cs_setup", "cs_setup_1", "seti_1", "cus_setup_1", "pm_setup_1");
        var adapter = new StripeGatewayAdapter(NullLogger<StripeGatewayAdapter>.Instance);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stripe-Signature"] = SignStripe(json, WebhookSecret)
        };

        var result = await adapter.ParseWebhookAsync("sk_test", WebhookSecret, json, headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_COMPLETED");
        result.EventId.Should().Be("evt_cs_setup");
        result.GatewayTransactionId.Should().Be("seti_1");
        result.AmountPaid.Should().Be(0m);
        result.GatewayCustomerId.Should().Be("cus_setup_1");
        result.GatewayTokenId.Should().Be("pm_setup_1");
    }

    [Test]
    public void ReadSetupSessionVaultIds_UnexpandedSetupIntent_LeavesPaymentMethodEmpty()
    {
        var session = new Session
        {
            Id = "cs_setup_str",
            CustomerId = null,
            SetupIntentId = "seti_unexpanded",
            SetupIntent = null
        };

        string? customerId = session.CustomerId;
        string? paymentMethodId = null;
        StripeGatewayAdapter.ReadSetupSessionVaultIds(session, ref customerId, ref paymentMethodId);

        paymentMethodId.Should().BeNull();
        var refused = StripeGatewayAdapter.RefuseSetupSessionWithoutToken(
            "evt_cs_setup_str", "MYR", session.SetupIntentId, new Dictionary<string, string>(), customerId);

        refused.Verified.Should().BeFalse();
        refused.EventType.Should().Be("PAYMENT_COMPLETED");
        refused.GatewayTokenId.Should().BeNull();
        refused.Error.Should().Contain("payment method");
    }

    [Test]
    public async Task ParseWebhook_SetupIntentSucceeded_ExtractsPaymentMethod()
    {
        var json = SetupIntentSucceededJson("evt_seti_ok", "seti_ok", "cus_ok", "pm_ok");
        var adapter = new StripeGatewayAdapter(NullLogger<StripeGatewayAdapter>.Instance);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stripe-Signature"] = SignStripe(json, WebhookSecret)
        };

        var result = await adapter.ParseWebhookAsync("sk_test", WebhookSecret, json, headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_COMPLETED");
        result.EventId.Should().Be("evt_seti_ok");
        result.GatewayTransactionId.Should().Be("seti_ok");
        result.AmountPaid.Should().Be(0m);
        result.GatewayCustomerId.Should().Be("cus_ok");
        result.GatewayTokenId.Should().Be("pm_ok");
    }

    [Test]
    public void TryMapSetupIntentSucceeded_MissingPaymentMethod_IsNotVerified()
    {
        var stripeEvent = new Event
        {
            Id = "evt_seti_bare",
            Type = "setup_intent.succeeded",
            Data = new EventData
            {
                Object = new SetupIntent { Id = "seti_bare", CustomerId = "cus_bare" }
            }
        };

        var result = StripeGatewayAdapter.TryMapSetupIntentSucceeded(stripeEvent);

        result.Should().NotBeNull();
        result!.Verified.Should().BeFalse();
        result.EventType.Should().Be("PAYMENT_COMPLETED");
        result.GatewayTokenId.Should().BeNull();
        result.Error.Should().Contain("payment method");
    }

    [Test]
    public void ReadSetupSessionVaultIds_WhenSetupIntentAndNoPi_ExtractsCustomerAndPaymentMethod()
    {
        var session = new Session
        {
            Id = "cs_setup_1",
            CustomerId = "cus_setup_1",
            SetupIntentId = "seti_1",
            SetupIntent = new SetupIntent
            {
                Id = "seti_1",
                CustomerId = "cus_setup_1",
                PaymentMethodId = "pm_setup_1"
            }
        };

        string? customerId = session.CustomerId;
        string? paymentMethodId = null;
        StripeGatewayAdapter.ReadSetupSessionVaultIds(session, ref customerId, ref paymentMethodId);

        customerId.Should().Be("cus_setup_1");
        paymentMethodId.Should().Be("pm_setup_1");
    }

    [Test]
    public async Task ParseWebhook_PaymentIntentSucceeded_UsesPaymentIntentId()
    {
        var json = PaymentIntentSucceededJson("evt_pi_1", "pi_test_1");
        var adapter = new StripeGatewayAdapter(NullLogger<StripeGatewayAdapter>.Instance);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stripe-Signature"] = SignStripe(json, WebhookSecret)
        };

        var result = await adapter.ParseWebhookAsync("sk_test", WebhookSecret, json, headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("PAYMENT_COMPLETED");
        result.EventId.Should().Be("evt_pi_1");
        result.GatewayTransactionId.Should().Be("pi_test_1");
    }

    [Test]
    public async Task ParseWebhook_UnmappedType_IsVerifiedWithStripeType()
    {
        var json = $$"""
            {
              "id": "evt_unmapped",
              "object": "event",
              "api_version": "{{StripeApiVersion}}",
              "request": null,
              "type": "customer.updated",
              "data": {
                "object": {
                  "id": "cus_1",
                  "object": "customer"
                }
              }
            }
            """;
        var adapter = new StripeGatewayAdapter(NullLogger<StripeGatewayAdapter>.Instance);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stripe-Signature"] = SignStripe(json, WebhookSecret)
        };

        var result = await adapter.ParseWebhookAsync("sk_test", WebhookSecret, json, headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("customer.updated");
        result.EventId.Should().Be("evt_unmapped");
    }

    [Test]
    public async Task ParseWebhook_RefundUpdatedSucceeded_IsRefundCompleted()
    {
        var json = RefundUpdatedJson("evt_re_1", "re_1", "pi_1", "succeeded", 4000);
        var adapter = new StripeGatewayAdapter(NullLogger<StripeGatewayAdapter>.Instance);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stripe-Signature"] = SignStripe(json, WebhookSecret)
        };

        var result = await adapter.ParseWebhookAsync("sk_test", WebhookSecret, json, headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("REFUND_COMPLETED");
        result.EventId.Should().Be("re_1");
        result.GatewayTransactionId.Should().Be("pi_1");
        result.AmountPaid.Should().Be(40m);
        result.Currency.Should().Be("MYR");
    }

    [Test]
    public async Task ParseWebhook_RefundUpdatedPending_IsNotCompleted()
    {
        var json = RefundUpdatedJson("evt_re_pend", "re_pend", "pi_1", "pending", 4000);
        var adapter = new StripeGatewayAdapter(NullLogger<StripeGatewayAdapter>.Instance);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stripe-Signature"] = SignStripe(json, WebhookSecret)
        };

        var result = await adapter.ParseWebhookAsync("sk_test", WebhookSecret, json, headers);

        result.Verified.Should().BeTrue();
        result.EventType.Should().Be("refund.updated");
    }

    private static string SetupSessionCompletedJson(
        string eventId,
        string sessionId,
        string setupIntentId,
        string customerId,
        string paymentMethodId) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "{{StripeApiVersion}}",
          "request": null,
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "{{sessionId}}",
              "object": "checkout.session",
              "mode": "setup",
              "amount_total": null,
              "currency": "myr",
              "customer": "{{customerId}}",
              "payment_intent": null,
              "setup_intent": {
                "id": "{{setupIntentId}}",
                "object": "setup_intent",
                "customer": "{{customerId}}",
                "payment_method": "{{paymentMethodId}}",
                "status": "succeeded"
              },
              "metadata": {}
            }
          }
        }
        """;

    private static string SetupIntentSucceededJson(
        string eventId,
        string setupIntentId,
        string customerId,
        string paymentMethodId) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "{{StripeApiVersion}}",
          "request": null,
          "type": "setup_intent.succeeded",
          "data": {
            "object": {
              "id": "{{setupIntentId}}",
              "object": "setup_intent",
              "customer": "{{customerId}}",
              "payment_method": "{{paymentMethodId}}",
              "status": "succeeded",
              "currency": "myr",
              "metadata": {}
            }
          }
        }
        """;

    private static string SessionCompletedJson(string eventId, string sessionId, string paymentIntentId) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "{{StripeApiVersion}}",
          "request": null,
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "{{sessionId}}",
              "object": "checkout.session",
              "amount_total": 5000,
              "currency": "myr",
              "payment_intent": "{{paymentIntentId}}",
              "metadata": {}
            }
          }
        }
        """;

    private static string RefundUpdatedJson(
        string eventId, string refundId, string paymentIntentId, string status, long amountMinor) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "{{StripeApiVersion}}",
          "request": null,
          "type": "refund.updated",
          "data": {
            "object": {
              "id": "{{refundId}}",
              "object": "refund",
              "amount": {{amountMinor}},
              "currency": "myr",
              "status": "{{status}}",
              "payment_intent": "{{paymentIntentId}}",
              "metadata": {}
            }
          }
        }
        """;

    private static string PaymentIntentSucceededJson(string eventId, string paymentIntentId) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "{{StripeApiVersion}}",
          "request": null,
          "type": "payment_intent.succeeded",
          "data": {
            "object": {
              "id": "{{paymentIntentId}}",
              "object": "payment_intent",
              "amount": 5000,
              "amount_received": 5000,
              "currency": "myr",
              "status": "succeeded",
              "metadata": {}
            }
          }
        }
        """;

    private static string SignStripe(string json, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var payload = timestamp + "." + json;
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"t={timestamp},v1={hex}";
    }
}
