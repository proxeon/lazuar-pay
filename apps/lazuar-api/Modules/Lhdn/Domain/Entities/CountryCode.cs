using BuildingBlocks.Domain;

namespace Modules.Lhdn.Domain.Entities;

public class CountryCode : Entity
{
    public string Code { get; private set; }
    public string CountryName { get; private set; }

#pragma warning disable CS8618
    private CountryCode() { }
#pragma warning restore CS8618

    public CountryCode(string code, string countryName)
    {
        Code = code.ToUpperInvariant().Trim();
        CountryName = countryName;
    }
}
