using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Rules;

public class InvalidSubscriptionStateTransitionRule : IBusinessRule
{
    private readonly string _currentState;
    private readonly string _targetState;
    private readonly bool _isReminderOnly;

    public InvalidSubscriptionStateTransitionRule(string currentState, string targetState, bool isReminderOnly)
    {
        _currentState = currentState.ToUpperInvariant();
        _targetState = targetState.ToUpperInvariant();
        _isReminderOnly = isReminderOnly;
    }

    public bool IsBroken()
    {
        // Rule: Reminder-only subscriptions cannot expire or suspend (they remain PAST_DUE)
        if (_isReminderOnly && (_targetState == "EXPIRED" || _targetState == "SUSPENDED"))
        {
            return true;
        }

        return (_currentState, _targetState) switch
        {
            ("PENDING", "ACTIVE") => false,
            ("PENDING", "CANCELLED") => false,
            
            ("ACTIVE", "PAST_DUE") => false,
            ("ACTIVE", "EXPIRED") => false,
            ("ACTIVE", "CANCELLED") => false,
            
            ("PAST_DUE", "ACTIVE") => false,
            ("PAST_DUE", "EXPIRED") => false,
            ("PAST_DUE", "CANCELLED") => false,
            
            ("EXPIRED", "ACTIVE") => false,
            ("EXPIRED", "CANCELLED") => false,
            
            _ => true // All other transitions are broken
        };
    }

    public string Message => 
        _isReminderOnly && (_targetState == "EXPIRED" || _targetState == "SUSPENDED")
        ? $"Cannot transition reminder-only subscription to {_targetState}. It must remain PAST_DUE indefinitely."
        : $"Invalid subscription state transition from {_currentState} to {_targetState}.";
}
