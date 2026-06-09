namespace BuildingBlocks.Domain;

public class GenericBusinessRule : IBusinessRule
{
    public string Message { get; }
    public GenericBusinessRule(string message) => Message = message;
    public bool IsBroken() => true;
}
