using Modules.Ops.Contracts;

namespace Modules.Billing.Application.Llm;

public class BillingPromptProvider : IAgentPromptProvider
{
    public string GetAppId() => "BILLING";

    public string GetSystemPromptRules()
    {
        return "**BILLING MODULE RULES (FINANCIAL TRUTH)**:\n" +
               "- When discussing revenue, strictly differentiate between 'Gross Revenue' (total catalog value of sales) and 'Net Cash in Bank' (actual cash deposited after deducting Gateway Fees like Stripe/Billplz).\n" +
               "- Always remind the user of 'Tax Liabilities' (SST/VAT) that are owed to the government and should not be counted as profit.\n" +
               "- Use the GetFinancialHealthAgentQuery tool for accurate ledger-based metrics.";
    }
}
