using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.ApiTypes;
using Lazuar.TestSupport;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.Services;
using Modules.CRM.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class CommerceDocumentLookupTests
{
    [Test]
    public async Task GetCustomerForDocument_FallsBackToCheckoutSessionCrm_WhenTransactionLogMissing()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, Guid.CreateVersion7(), null, DateTime.UtcNow.AddHours(1));
        db.CheckoutSessions.Add(session);
        await db.SaveChangesAsync();

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Aisha Merchant",
            Email = "aisha@example.com"
        });

        var lookup = new CommerceDocumentLookup(Substitute.For<ISqlConnectionFactory>(), crm, db);

        var customer = await lookup.GetCustomerForDocumentAsync(orgId, "gw_txn_missing", session.Id.ToString());

        customer.Should().NotBeNull();
        customer!.Email.Should().Be("aisha@example.com");
        customer.Name.Should().Be("Aisha Merchant");
    }

    [Test]
    public async Task GetCustomerForDocument_FallsBackToSubscriptionCrm()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var sub = new Subscription(orgId, clientId, Guid.CreateVersion7());
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Renewal Buyer",
            Email = "renewal@example.com"
        });

        var lookup = new CommerceDocumentLookup(Substitute.For<ISqlConnectionFactory>(), crm, db);

        var customer = await lookup.GetCustomerForDocumentAsync(orgId, sub.Id.ToString(), sub.Id.ToString());

        customer.Should().NotBeNull();
        customer!.Email.Should().Be("renewal@example.com");
        customer.Name.Should().Be("Renewal Buyer");
    }

    [Test]
    public async Task GetCustomerForDocument_PrefersCrmOverTransactionLog_WhenLogHasEmailOnly()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, Guid.CreateVersion7(), null, DateTime.UtcNow.AddHours(1));
        db.CheckoutSessions.Add(session);
        db.TransactionLogs.Add(new CommerceTransactionLog(
            orgId,
            100m,
            2m,
            "MYR",
            CommerceTransactionLog.StatusConfirmed,
            "Person From Checkout",
            "aisha@example.com",
            "Plan",
            "system",
            "gw_txn_paid"));
        await db.SaveChangesAsync();

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Aisha Merchant",
            Email = "aisha@example.com",
            Tin = "C12345678901",
            Company_name = "Buyer Sdn Bhd",
            Id_type = "BRN",
            Id_value = "202401001234"
        });

        var lookup = new CommerceDocumentLookup(Substitute.For<ISqlConnectionFactory>(), crm, db);

        var customer = await lookup.GetCustomerForDocumentAsync(orgId, "gw_txn_paid", session.Id.ToString());

        customer.Should().NotBeNull();
        customer!.Tin.Should().Be("C12345678901");
        customer.CompanyName.Should().Be("Buyer Sdn Bhd");
        customer.IdType.Should().Be("BRN");
        customer.IdValue.Should().Be("202401001234");
    }

    private static CommerceDbContext CreateDb() =>
        new(
            InMemoryDb.CreateOptions<CommerceDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
}
