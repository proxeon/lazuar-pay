using Lazuar.ApiTypes;
using Modules.Commerce.Contracts;
using Modules.Lhdn.Infrastructure.Services;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class LhdnBuyerMapperTests
{
    [Test]
    public void Checkout_Display_Wins_Over_Crm_Profile()
    {
        var profile = new ClientProfileDto
        {
            Id = Guid.CreateVersion7().ToString(),
            Full_name = "First Buyer",
            Email = "shared@example.com",
            Phone = "60111111111",
            Tin = "IG99999999999",
            Id_type = "NRIC",
            Id_value = "900101145678"
        };
        var display = new CommerceCustomerDisplay(
            "Second Buyer",
            "shared@example.com",
            Tin: "C12345678901",
            CompanyName: "Other Sdn Bhd",
            IdType: "BRN",
            IdValue: "202401001234");

        var ok = LhdnBuyerMapper.TryCreatePayloadBuyer(
            profile,
            display,
            out var buyerName,
            out var buyerTin,
            out var idType,
            out var idValue,
            out _);

        Assert.That(ok, Is.True);
        Assert.That(buyerTin, Is.EqualTo("C12345678901"));
        Assert.That(buyerName, Is.EqualTo("Other Sdn Bhd"));
        Assert.That(idType, Is.EqualTo(SubmitDocumentRequestDtoBuyer_id_type.BRN));
        Assert.That(idValue, Is.EqualTo("202401001234"));
    }
}
