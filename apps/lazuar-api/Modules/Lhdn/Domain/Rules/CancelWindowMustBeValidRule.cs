using System;
using BuildingBlocks.Domain;

namespace Modules.Lhdn.Domain.Rules;

/// <summary>
/// Enforces LHDN's 72-hour cancellation policy.
/// </summary>
public class CancelWindowMustBeValidRule : IBusinessRule
{
    private readonly DateTime? _validatedAt;
    private const int AllowedHours = 72;

    public CancelWindowMustBeValidRule(DateTime? validatedAt)
    {
        _validatedAt = validatedAt;
    }

    public bool IsBroken()
    {
        if (!_validatedAt.HasValue) return false;
        
        return DateTime.UtcNow > _validatedAt.Value.AddHours(AllowedHours);
    }

    public string Message => $"Documents can only be cancelled within {AllowedHours} hours of successful validation.";
}
