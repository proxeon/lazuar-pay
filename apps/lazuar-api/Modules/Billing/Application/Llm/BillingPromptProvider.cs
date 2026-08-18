using Modules.Ops.Contracts;

namespace Modules.Billing.Application.Llm;

public class BillingPromptProvider : IAgentPromptProvider
{
    public string GetAppId() => "BILLING";

    public string GetSystemPromptRules()
    {
        return "**BILLING MODULE RULES (FINANCIAL TRUTH)**:\n" +
               "- When discussing revenue, strictly differentiate between 'Gross Revenue' (catalog sales) and 'Net revenue' (P&L after booked gateway fees and tax). Do not call that figure cash in the bank.\n" +
               "- Always remind the user of 'Tax Liabilities' (SST/VAT) that are owed to the government and should not be counted as profit.\n" +
               "- Use the GetFinancialHealthAgentQuery tool for accurate ledger-based metrics.";
    }
}
