using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.IntegrationTests;

[TestFixture]
public class BillingDbContextTests
{
    private BillingDbContext _dbContext = null!;
    private Guid _tenantId;

    [SetUp]
    public void SetUp()
    {
        _tenantId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var executionContext = Substitute.For<IExecutionContextAccessor>();
        executionContext.TenantId.Returns(_tenantId);

        var mediator = Substitute.For<IMediator>();
        var jobTrigger = new DatabaseJobTrigger();

        _dbContext = new BillingDbContext(options, executionContext, mediator, jobTrigger);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    [Test]
    public async Task SaveChangesAsync_WhenAddingChildEntityWithPreAssignedIdToExistingAggregate_ShouldSaveSuccessfully()
    {
        // Arrange - Phase 1: Simulate a previous HTTP request creating the wallet
        // CRITICAL FIX: Use the _tenantId attached to the execution context so global query filters pass
        var wallet = new TenantCreditBalance(_tenantId);
        
        _dbContext.TenantCreditBalances.Add(wallet);
        await _dbContext.SaveChangesAsync();

        // Clear the EF Core ChangeTracker to simulate a brand new HTTP request
        _dbContext.ChangeTracker.Clear();

        // Act - Phase 2: Load the existing aggregate and append a transaction
        var existingWallet = await _dbContext.TenantCreditBalances.FirstAsync(w => w.Id == wallet.Id);
        
        // TopUp instantiates a CreditLedger (which generates its own Guid.CreateVersion7() ID internally)
        var action = async () => 
        {
            existingWallet.TopUp(500, "Test TopUp");
            await _dbContext.SaveChangesAsync();
        };

        // Assert - If DbContext interception is missing, this throws DbUpdateConcurrencyException
        await action.Should().NotThrowAsync();
        
        var savedLedgers = await _dbContext.CreditLedgers.Where(cl => cl.TenantCreditBalanceId == existingWallet.Id).ToListAsync();
        savedLedgers.Should().HaveCount(1);
        savedLedgers.First().Amount.Should().Be(500);
    }
}
