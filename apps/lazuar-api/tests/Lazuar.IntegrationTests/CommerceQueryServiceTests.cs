using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.Services;
using Modules.CRM.Contracts;
using NSubstitute;
using NUnit.Framework;
using Testcontainers.PostgreSql;

namespace Lazuar.IntegrationTests.Commerce;

[TestFixture]
public class CommerceQueryServiceTests
{
    private PostgreSqlContainer _dbContainer = null!;
    private CommerceDbContext _dbContext = null!;
    private CommerceQueryService _queryService = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
#pragma warning disable CS0618
        _dbContainer = new PostgreSqlBuilder()
            .WithDatabase("lazuar_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
#pragma warning restore CS0618

        await _dbContainer.StartAsync();

        var connectionString = _dbContainer.GetConnectionString();

        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "commerce");
            })
            // Ignore strict pending model changes warning in tests
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var executionContext = Substitute.For<IExecutionContextAccessor>();
        executionContext.TenantId.Returns(Guid.CreateVersion7());

        var mediator = Substitute.For<IMediator>();
        var jobTrigger = new DatabaseJobTrigger();

        _dbContext = new CommerceDbContext(options, executionContext, mediator, jobTrigger);

        await _dbContext.Database.MigrateAsync();

        var connectionFactory = new NpgsqlConnectionFactory(connectionString);
        var crmQueryService = Substitute.For<ICrmQueryService>();

        _queryService = new CommerceQueryService(connectionFactory, crmQueryService);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _dbContext.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    [Test]
    public void DapperQueries_ShouldMatchEntityFrameworkSchema()
    {
        var orgId = Guid.CreateVersion7();

        Assert.DoesNotThrowAsync(async () => await _queryService.GetProductsAsync(orgId));
        Assert.DoesNotThrowAsync(async () => await _queryService.GetCouponsAsync(orgId));
        Assert.DoesNotThrowAsync(async () => await _queryService.GetSubscribersAsync(orgId, 1, 50, null));
        Assert.DoesNotThrowAsync(async () => await _queryService.GetTransactionsAsync(orgId, 1, 50, null, null, null));
        Assert.DoesNotThrowAsync(async () => await _queryService.GetDunningCampaignsAsync(orgId));
        Assert.DoesNotThrowAsync(async () => await _queryService.GetStatsAsync(orgId));
        Assert.DoesNotThrowAsync(async () => await _queryService.GetPortalDataAsync(orgId, Guid.CreateVersion7()));
        Assert.DoesNotThrowAsync(async () => await _queryService.GetCustomCheckoutsAsync(orgId, 1, 50));
        Assert.DoesNotThrowAsync(async () => await _queryService.GetCheckoutStatusAsync(orgId, Guid.CreateVersion7()));
    }

    [Test]
    public async Task GetCheckoutStatusAsync_PollerContract_CompletedPendingExpiredAndMissing()
    {
        var orgId = Guid.CreateVersion7();
        var otherOrgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();

        var open = new CheckoutSession(orgId, clientId, productId, couponId: null, DateTime.UtcNow.AddHours(1));
        var completed = new CheckoutSession(orgId, clientId, productId, couponId: null, DateTime.UtcNow.AddHours(1));
        completed.Complete();
        var expired = new CheckoutSession(orgId, clientId, productId, couponId: null, DateTime.UtcNow.AddHours(-1));
        expired.Expire();

        _dbContext.CheckoutSessions.AddRange(open, completed, expired);
        await _dbContext.SaveChangesAsync();

        var missing = await _queryService.GetCheckoutStatusAsync(orgId, Guid.CreateVersion7());
        missing.Should().BeNull();

        var wrongOrg = await _queryService.GetCheckoutStatusAsync(otherOrgId, completed.Id);
        wrongOrg.Should().BeNull();

        var pending = await _queryService.GetCheckoutStatusAsync(orgId, open.Id);
        pending.Should().NotBeNull();
        pending!.Status.Should().Be("PENDING");
        pending.Token.Should().BeNull();

        var done = await _queryService.GetCheckoutStatusAsync(orgId, completed.Id);
        done.Should().NotBeNull();
        done!.Status.Should().Be("COMPLETED");
        done.Token.Should().BeNull();

        var dead = await _queryService.GetCheckoutStatusAsync(orgId, expired.Id);
        dead.Should().NotBeNull();
        dead!.Status.Should().Be("EXPIRED");
        dead.Status.Should().NotBe("COMPLETED");
        dead.Token.Should().BeNull();
    }

    [Test]
    public async Task GetStatsAsync_SumsCampaignRecoveredRevenueAndSavedSubscriptions()
    {
        var orgId = Guid.CreateVersion7();
        var otherOrgId = Guid.CreateVersion7();

        var first = new DunningCampaign(orgId, "Primary", "SUSPEND", 7);
        first.RecordRecovery(100m);
        var second = new DunningCampaign(orgId, "Secondary", "NONE", 14);
        second.RecordRecovery(10m);
        second.RecordRecovery(10.5m);
        var otherOrg = new DunningCampaign(otherOrgId, "Other", "CANCEL", 7);
        otherOrg.RecordRecovery(999m);
        otherOrg.RecordRecovery(1m);
        otherOrg.RecordRecovery(1m);

        _dbContext.DunningCampaigns.AddRange(first, second, otherOrg);
        await _dbContext.SaveChangesAsync();

        var stats = await _queryService.GetStatsAsync(orgId);
        stats.Recovered_revenue.Should().Be(120.5);
        stats.Saved_subscriptions.Should().Be(3);

        var empty = await _queryService.GetStatsAsync(Guid.CreateVersion7());
        empty.Recovered_revenue.Should().Be(0);
        empty.Saved_subscriptions.Should().Be(0);
    }
}
