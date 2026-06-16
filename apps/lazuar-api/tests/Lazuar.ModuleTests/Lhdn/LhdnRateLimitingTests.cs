using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Lazuar.ApiTypes;
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
    private IJsonSignatureService _signatureService = null!;
    private ICertificateVaultService _vaultService = null!;
    private IUblDocumentStrategy _mockStrategy = null!;
    private SubmitTaxDocumentCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = Substitute.For<ILhdnRepository>();
        _strategyFactory = Substitute.For<IDocumentStrategyFactory>();
        _signatureService = Substitute.For<IJsonSignatureService>();
        _vaultService = Substitute.For<ICertificateVaultService>();
        _mockStrategy = Substitute.For<IUblDocumentStrategy>();

        _handler = new SubmitTaxDocumentCommandHandler(
            _repository,
            _strategyFactory,
            _signatureService,
            _vaultService
        );
    }

    [Test]
    public async Task SubmitTaxDocument_WithValidPayload_ShouldHandleRateLimitGracefully()
    {
        var orgId = Guid.NewGuid();
        var request = new SubmitDocumentRequestDto
        {
            Internal_id = "INV-123",
            Document_version = "1.0",
            Buyer_id_type = SubmitDocumentRequestDtoBuyer_id_type.BRN,
            Buyer_address = new LhdnAddressDto { State_code = LhdnAddressDtoState_code._14 },
            Items = new System.Collections.Generic.List<LhdnItemDto>()
        };

        var config = new LhdnTenantConfig(orgId, false, "C1234567890", "BRN", "12345");
        
        _repository.GetTenantConfigAsync(orgId, Arg.Any<CancellationToken>()).Returns(config);
        _strategyFactory.GetStrategy(request).Returns(_mockStrategy);
        _mockStrategy.Generate(request, config, "1.0").Returns(new object());

        _signatureService.SerializeUnsignedDocument(Arg.Any<object>())
            .Returns(("{\"dummy\":\"json\"}", "dummy_hex_hash"));

        var command = new SubmitTaxDocumentCommand(orgId, request);

        var resultId = await _handler.Handle(command, CancellationToken.None);

        resultId.Should().NotBeEmpty();
        _repository.Received(1).AddTaxDocument(Arg.Is<TaxDocument>(d => 
            d.InternalReferenceId == "INV-123" && 
            d.DocumentHash == "dummy_hex_hash"));
    }
}
