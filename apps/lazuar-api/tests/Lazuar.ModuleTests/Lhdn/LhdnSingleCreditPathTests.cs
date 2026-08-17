using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Lhdn.Application;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class LhdnSingleCreditPathTests
{
    private ILhdnRepository _repository = null!;
    private IDocumentStrategyFactory _strategyFactory = null!;
    private IUblValidatorService _validatorService = null!;
    private IExecutionContextAccessor _executionContext = null!;
    private IBillingQueryService _billingQueryService = null!;
    private ICreditCostService _creditCostService = null!;
    private IMediator _mediator = null!;
    private SubmitTaxDocumentCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = Substitute.For<ILhdnRepository>();
        _strategyFactory = Substitute.For<IDocumentStrategyFactory>();
        _validatorService = Substitute.For<IUblValidatorService>();
        _executionContext = Substitute.For<IExecutionContextAccessor>();
        _billingQueryService = Substitute.For<IBillingQueryService>();
        _creditCostService = Substitute.For<ICreditCostService>();
        _mediator = Substitute.For<IMediator>();

        _executionContext.IsTestMode.Returns(false);
        _billingQueryService.HasSufficientCreditsAsync(Arg.Any<Guid>(), Arg.Any<int>()).Returns(true);
        _creditCostService.GetCost(CreditAction.LhdnSubmit).Returns(3);

        var tin = Substitute.For<ITaxpayerValidationService>();
        tin.ValidateTinAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TinValidationResponse(true, "C55555555555", "Buyer"));
        _handler = new SubmitTaxDocumentCommandHandler(
            _repository,
            _strategyFactory,
            _validatorService,
            _executionContext,
            _billingQueryService,
            _creditCostService,
            _mediator,
            Substitute.For<ILogger<SubmitTaxDocumentCommandHandler>>(),
            tin,
            Substitute.For<IDocumentSigner>(),
            Substitute.For<ICertificateVaultService>(),
            Options.Create(new LhdnSigningOptions()));
    }

    [Test]
    public async Task Handle_UsesCreditCostService_AndDeductsConfiguredAmountOnce()
    {
        var orgId = Guid.CreateVersion7();
        var idempotencyKey = "idem-lhdn-1";
        ArrangeValidSubmit(orgId);

        await _handler.Handle(new SubmitTaxDocumentCommand(orgId, idempotencyKey, BuildPayload()), CancellationToken.None);

        _creditCostService.Received(1).GetCost(CreditAction.LhdnSubmit);
        await _billingQueryService.Received(1).HasSufficientCreditsAsync(orgId, 3);
        await _mediator.Received(1).Send(
            Arg.Is<DeductTenantCreditCommand>(c =>
                c.OrganizationId == orgId
                && c.Amount == 3
                && c.IdempotencyKey == $"lhdn:{idempotencyKey}"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_TestMode_DoesNotDeductCredits()
    {
        var orgId = Guid.CreateVersion7();
        _executionContext.IsTestMode.Returns(true);
        ArrangeValidSubmit(orgId);

        await _handler.Handle(new SubmitTaxDocumentCommand(orgId, "idem-test", BuildPayload()), CancellationToken.None);

        _creditCostService.Received(1).GetCost(CreditAction.LhdnSubmit);
        await _billingQueryService.DidNotReceive()
            .HasSufficientCreditsAsync(Arg.Any<Guid>(), Arg.Any<int>());
        await _mediator.DidNotReceive().Send(Arg.Any<DeductTenantCreditCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_DoesNotUseHardcodedOneCredit_WhenCostIsConfiguredHigher()
    {
        var orgId = Guid.CreateVersion7();
        _creditCostService.GetCost(CreditAction.LhdnSubmit).Returns(5);
        ArrangeValidSubmit(orgId);

        await _handler.Handle(new SubmitTaxDocumentCommand(orgId, "idem-5", BuildPayload()), CancellationToken.None);

        await _billingQueryService.Received(1).HasSufficientCreditsAsync(orgId, 5);
        await _mediator.Received(1).Send(
            Arg.Is<DeductTenantCreditCommand>(c => c.Amount == 5),
            Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(
            Arg.Is<DeductTenantCreditCommand>(c => c.Amount == 1),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_LhdnCostZero_DoesNotDeductOrPrecheck()
    {
        var orgId = Guid.CreateVersion7();
        _creditCostService.GetCost(CreditAction.LhdnSubmit).Returns(0);
        ArrangeValidSubmit(orgId);

        await _handler.Handle(new SubmitTaxDocumentCommand(orgId, "idem-zero", BuildPayload()), CancellationToken.None);

        _creditCostService.Received(1).GetCost(CreditAction.LhdnSubmit);
        await _billingQueryService.DidNotReceive()
            .HasSufficientCreditsAsync(Arg.Any<Guid>(), Arg.Any<int>());
        await _mediator.DidNotReceive().Send(
            Arg.Is<DeductTenantCreditCommand>(c => c.Amount == 0),
            Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(Arg.Any<DeductTenantCreditCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_LiveMode_CostGreaterThanZero_StillDeducts()
    {
        var orgId = Guid.CreateVersion7();
        var idempotencyKey = "idem-live-positive";
        _creditCostService.GetCost(CreditAction.LhdnSubmit).Returns(3);
        ArrangeValidSubmit(orgId);

        await _handler.Handle(new SubmitTaxDocumentCommand(orgId, idempotencyKey, BuildPayload()), CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<DeductTenantCreditCommand>(c =>
                c.OrganizationId == orgId
                && c.Amount > 0
                && c.Amount == 3
                && c.IdempotencyKey == $"lhdn:{idempotencyKey}"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void Handle_DeductFails_DoesNotPersistDocument()
    {
        var orgId = Guid.CreateVersion7();
        ArrangeValidSubmit(orgId);
        _mediator.Send(Arg.Any<DeductTenantCreditCommand>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("402: Insufficient API Credits"));

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new SubmitTaxDocumentCommand(orgId, "idem-fail", BuildPayload()), CancellationToken.None));

        _repository.DidNotReceive().AddTaxDocument(Arg.Any<TaxDocument>());
        _repository.DidNotReceive().AddIdempotencyLog(Arg.Any<Modules.Lhdn.Domain.Entities.IdempotencyLog>());
    }

    private void ArrangeValidSubmit(Guid orgId)
    {
        var config = new LhdnTenantConfig(orgId, false, "C1234567890", "BRN", "20200101");
        _repository.GetTenantConfigAsync(orgId, Arg.Any<CancellationToken>()).Returns(config);

        var strategy = Substitute.For<IUblDocumentStrategy>();
        strategy.Generate(Arg.Any<SubmitDocumentRequestDto>(), config, "1.0").Returns("<Invoice></Invoice>");
        _strategyFactory.GetStrategy(Arg.Any<SubmitDocumentRequestDto>()).Returns(strategy);
    }

    private static SubmitDocumentRequestDto BuildPayload() => new()
    {
        Internal_id = "INV-CREDIT-1",
        Document_type = SubmitDocumentRequestDtoDocument_type._01,
        Issue_date = DateTimeOffset.UtcNow,
        Buyer_name = "Test Buyer",
        Buyer_tin = "C55555555555",
        Buyer_id_type = SubmitDocumentRequestDtoBuyer_id_type.BRN,
        Buyer_id_value = "20200101",
        Buyer_address = new LhdnAddressDto
        {
            Line1 = "Test Address",
            City = "KL",
            Postal_code = "50000",
            State_code = LhdnAddressDtoState_code._14,
            Country_code = "MYS"
        },
        Items = new List<LhdnItemDto>(),
        Total_excluding_tax = 100,
        Total_tax = 0,
        Total_including_tax = 100
    };
}
