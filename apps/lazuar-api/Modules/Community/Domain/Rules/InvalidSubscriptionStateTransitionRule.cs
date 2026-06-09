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
        if (_currentState == "BANNED" && _targetState != "BANNED")
        {
            return true;
        }

        if (_targetState == "BANNED")
        {
            return false;
        }

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
            
            _ => true 
        };
    }

    public string Message => 
        _currentState == "BANNED" ? "Cannot transition from BANNED state. This is a terminal state." :
        _isReminderOnly && (_targetState == "EXPIRED" || _targetState == "SUSPENDED")
        ? $"Cannot transition reminder-only subscription to {_targetState}. It must remain PAST_DUE indefinitely."
        : $"Invalid subscription state transition from {_currentState} to {_targetState}.";
}
