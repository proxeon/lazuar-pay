using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Community.Domain.Aggregates;
using Modules.Community.Infrastructure;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.IntegrationTests;

[TestFixture]
public class CommunityDbContextTests
{
    private CommunityDbContext _dbContext = null!;
    private Guid _tenantId;

    [SetUp]
    public void SetUp()
    {
        _tenantId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<CommunityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var executionContext = Substitute.For<IExecutionContextAccessor>();
        executionContext.TenantId.Returns(_tenantId);

        var mediator = Substitute.For<IMediator>();
        var jobTrigger = new DatabaseJobTrigger();

        _dbContext = new CommunityDbContext(options, executionContext, mediator, jobTrigger);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    [Test]
    public async Task SaveChangesAsync_WhenAddingPaymentRecordToExistingSubscription_ShouldSaveSuccessfully()
    {
        // Arrange - Phase 1: Create the subscription
        // CRITICAL FIX: Use the _tenantId attached to the execution context so global query filters pass
        var subscription = new CommunitySubscription(
            _tenantId, Guid.NewGuid(), Guid.NewGuid(), "MANUAL", false, null, null);
        
        _dbContext.Subscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();

        // Act - Phase 2: Load the subscription and activate it (which adds a PaymentRecord child entity)
        var existingSub = await _dbContext.Subscriptions.FirstAsync(s => s.Id == subscription.Id);
        
        var action = async () => 
        {
            existingSub.Activate(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), 99m, "MYR", "CASH", null, "TEST");
            await _dbContext.SaveChangesAsync();
        };

        // Assert
        await action.Should().NotThrowAsync();
        
        var savedRecords = await _dbContext.PaymentRecords.Where(pr => pr.SubscriptionId == existingSub.Id).ToListAsync();
        savedRecords.Should().HaveCount(1);
        savedRecords.First().Amount.Should().Be(99m);
    }
}
