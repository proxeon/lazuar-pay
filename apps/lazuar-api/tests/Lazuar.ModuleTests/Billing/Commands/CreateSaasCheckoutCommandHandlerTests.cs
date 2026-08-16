using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using Lazuar.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.Commands;
using Modules.Billing.Infrastructure.Services;
using Modules.One.Contracts;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Queries;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.Commands;

[TestFixture]
public class CreateSaasCheckoutCommandHandlerTests
{
    private BillingDbContext _db = null!;
    private IMediator _mediator = null!;
    private IOneQueryService _one = null!;
    private Guid _tenantId;

    [SetUp]
    public void SetUp()
    {
        _tenantId = Guid.CreateVersion7();
        _db = new BillingDbContext(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.ForTenant(_tenantId),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
        _mediator = Substitute.For<IMediator>();
        _one = Substitute.For<IOneQueryService>();
        _one.GetWorkspaceMembersAsync(_tenantId).Returns(new[]
        {
            new WorkspaceMemberSnapshotDto(Guid.CreateVersion7(), Guid.CreateVersion7(), "Ada", "ada@example.com", "ADMIN", DateTime.UtcNow)
        });
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private CreateSaasCheckoutCommandHandler Handler(decimal amount = 99m) =>
        new(_db, _mediator, _one, Options.Create(new SaasOptions
        {
            Plan = new SaasPlanOptions
            {
                Code = "hub_starter",
                Name = "Hub Starter",
                AmountMyr = amount,
                Interval = "mo",
                Currency = "MYR"
            }
        }));

    [Test]
    public void Handle_AmountNotConfigured_Throws()
    {
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(0).Handle(new CreateSaasCheckoutCommand(_tenantId, "https://ops/billing"), CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("not configured"));
    }

    [Test]
    public void Handle_SystemOrg_Throws()
    {
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler().Handle(
                new CreateSaasCheckoutCommand(PlatformCheckoutTypes.SystemOrganizationId, "https://ops/billing"),
                CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("System organization"));
    }

    [Test]
    public async Task Handle_SetsPlatformSaasFeeMetadata_AndUpsertsUnpaid()
    {
        GenerateSystemCheckoutSessionQuery? captured = null;
        _mediator.Send(Arg.Any<GenerateSystemCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                captured = ci.Arg<GenerateSystemCheckoutSessionQuery>();
                return "https://pay.example/saas";
            });

        var url = await Handler().Handle(
            new CreateSaasCheckoutCommand(_tenantId, "https://ops/billing"),
            CancellationToken.None);

        Assert.That(url, Is.EqualTo("https://pay.example/saas"));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Metadata["type"], Is.EqualTo(PlatformCheckoutTypes.PlatformSaasFee));
        Assert.That(captured.Metadata["tenant_id"], Is.EqualTo(_tenantId.ToString()));
        Assert.That(captured.Metadata["plan_code"], Is.EqualTo("hub_starter"));
        Assert.That(captured.Amount, Is.EqualTo(99m));
        Assert.That(captured.Metadata.ContainsKey("type") && captured.Metadata["type"] != PlatformCheckoutTypes.UtilityCreditTopup);

        var row = await _db.WorkspaceSaasSubscriptions.SingleAsync(s => s.OrganizationId == _tenantId);
        Assert.That(row.Status, Is.EqualTo(WorkspaceSaasStatuses.Unpaid));
        Assert.That(row.CurrentPeriodEnd, Is.Null);
    }

    [Test]
    public async Task Handle_ExistingActive_DoesNotResetToUnpaid()
    {
        var existing = new WorkspaceSaasSubscription(_tenantId, "hub_starter");
        existing.ActivateFromPayment(DateTime.UtcNow, "mo", "prior");
        _db.WorkspaceSaasSubscriptions.Add(existing);
        await _db.SaveChangesAsync();

        _mediator.Send(Arg.Any<GenerateSystemCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://pay.example/saas");

        await Handler().Handle(
            new CreateSaasCheckoutCommand(_tenantId, "https://ops/billing"),
            CancellationToken.None);

        var row = await _db.WorkspaceSaasSubscriptions.SingleAsync(s => s.OrganizationId == _tenantId);
        Assert.That(row.Status, Is.EqualTo(WorkspaceSaasStatuses.Active));
    }
}
