using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Billing.Contracts;
using Modules.Lhdn.Application;
using Modules.Billing.Contracts.Commands;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class LhdnRateLimitingTests
{
    private ILhdnRepository _repository = null!;
    private IDocumentStrategyFactory _strategyFactory = null!;
    private IUblValidatorService _validatorService = null!;
    private IExecutionContextAccessor _executionContext = null!;
    private IBillingQueryService _billingQueryService = null!;
    private ICreditCostService _creditCostService = null!;
    private IMediator _mediator = null!;
    private ILogger<SubmitTaxDocumentCommandHandler> _logger = null!;
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
        _logger = Substitute.For<ILogger<SubmitTaxDocumentCommandHandler>>();

        _executionContext.IsTestMode.Returns(false);
        _billingQueryService.HasSufficientCreditsAsync(Arg.Any<Guid>(), Arg.Any<int>()).Returns(true);
        _creditCostService.GetCost(Arg.Any<CreditAction>()).Returns(3);

        var tin = Substitute.For<ITaxpayerValidationService>();
        tin.ValidateTinAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TinValidationResponse(true, "IG1234567890", "Buyer"));
        _handler = new SubmitTaxDocumentCommandHandler(
            _repository,
            _strategyFactory,
            _validatorService,
            _executionContext,
            _billingQueryService,
            _creditCostService,
            _mediator,
            _logger,
            tin,
            Substitute.For<IDocumentSigner>(),
            Substitute.For<ICertificateVaultService>(),
            Options.Create(new LhdnSigningOptions()));
    }

    [Test]
    public async Task Handle_ShouldSaveDocument_WhenValidPayloadIsProvided()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString();

        var config = new LhdnTenantConfig(orgId, false, "C1234567890", "BRN", "20200101");
        _repository.GetTenantConfigAsync(orgId, Arg.Any<CancellationToken>()).Returns(config);

        var strategy = Substitute.For<IUblDocumentStrategy>();
        strategy.Generate(Arg.Any<SubmitDocumentRequestDto>(), config, "1.0").Returns("<Invoice></Invoice>");
        _strategyFactory.GetStrategy(Arg.Any<SubmitDocumentRequestDto>()).Returns(strategy);

        var payload = new SubmitDocumentRequestDto
        {
            Internal_id = "INV-123",
            Document_type = SubmitDocumentRequestDtoDocument_type._01,
            Issue_date = DateTimeOffset.UtcNow,
            Buyer_name = "Test Buyer",
            Buyer_tin = "IG1234567890",
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
            Items = new System.Collections.Generic.List<LhdnItemDto>(),
            Total_excluding_tax = 100,
            Total_tax = 0,
            Total_including_tax = 100
        };

        var command = new SubmitTaxDocumentCommand(orgId, idempotencyKey, payload);

        // Act
        var resultId = await _handler.Handle(command, CancellationToken.None);

        // Assert
        resultId.Should().NotBeEmpty();
        _repository.Received(1).AddTaxDocument(Arg.Is<TaxDocument>(d => d.InternalReferenceId == "INV-123"));
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
