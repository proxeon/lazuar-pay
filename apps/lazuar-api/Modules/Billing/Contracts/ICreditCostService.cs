using System;
using System.Collections.Generic;

namespace Modules.Billing.Contracts;

/// <summary>Billable high-value actions that consume utility credits.</summary>
public enum CreditAction
{
    EmailSend,
    WhatsAppSend,
    LhdnSubmit,
    BroadcastEmailPerRecipient
}

/// <summary>
/// Resolves the credit cost of a billable action and the purchasable credit packages.
/// Rates are config-driven (appsettings "Credits" section) so they can be tuned without code changes.
/// </summary>
public interface ICreditCostService
{
    /// <summary>Credit cost for a single invocation of the given action.</summary>
    int GetCost(CreditAction action);

    /// <summary>Purchasable top-up packages, ordered by ascending price.</summary>
    IReadOnlyList<CreditPackage> GetPackages();

    /// <summary>One-time free credits granted to a new tenant.</summary>
    int GetStarterGrant();
}

/// <summary>A purchasable credit top-up package.</summary>
public record CreditPackage(decimal AmountMyr, int Credits);
