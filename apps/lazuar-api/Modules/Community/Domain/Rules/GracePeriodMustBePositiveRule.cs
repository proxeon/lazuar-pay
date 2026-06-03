using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Rules;

public class GracePeriodMustBePositiveRule : IBusinessRule
{
    private readonly int _gracePeriodDays;

    public GracePeriodMustBePositiveRule(int gracePeriodDays)
    {
        _gracePeriodDays = gracePeriodDays;
    }

    public bool IsBroken() => _gracePeriodDays < 0;

    public string Message => "Grace period days cannot be negative.";
}
