using BuildingBlocks.Domain;

namespace Modules.Lhdn.Domain.Entities;

public class TaxType : Entity
{
    public string Code { get; private set; }
    public string Description { get; private set; }

#pragma warning disable CS8618
    private TaxType() { }
#pragma warning restore CS8618

    public TaxType(string code, string description)
    {
        Code = code.Trim();
        Description = description;
    }
}
