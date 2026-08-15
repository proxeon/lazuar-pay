using System.Text.Json;
using FluentAssertions;
using Modules.Payments.Infrastructure.Gateways;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class ChipCollectGatewayAdapterTests
{
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
}
