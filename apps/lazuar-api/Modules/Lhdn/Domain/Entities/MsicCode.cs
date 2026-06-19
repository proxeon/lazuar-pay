using BuildingBlocks.Domain;

namespace Modules.Lhdn.Domain.Entities;

public class MsicCode : Entity
{
    public string Code { get; private set; }
    public string Description { get; private set; }
    public string CategoryReference { get; private set; }

#pragma warning disable CS8618
    private MsicCode() { }
#pragma warning restore CS8618

    public MsicCode(string code, string description, string categoryReference)
    {
        Code = code.Trim();
        Description = description;
        CategoryReference = categoryReference;
    }
}
