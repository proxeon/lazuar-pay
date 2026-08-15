using System.Globalization;

namespace Modules.Communications.Application;

public sealed record MessageTemplateContext(
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string BusinessName,
    string PlanName,
    string Amount,
    string TotalPrice,
    string Currency,
    string DaysOverdue,
    string CurrentPeriodEnd,
    string RenewalLink,
    string PortalMagicLink,
    string UpdatePaymentLink);

public static class MessageLinkBuilder
{
    public static (string RenewalLink, string PortalMagicLink, string UpdatePaymentLink) Build(
        string? clientUrl,
        string? slug,
        string? subscriptionId,
        string? magicToken)
    {
        var portalBase = (clientUrl ?? "").TrimEnd('/');
        var workspaceSlug = slug ?? "";
        var subId = subscriptionId ?? "";
        var updatePaymentLink = string.IsNullOrEmpty(subId)
            ? $"{portalBase}/{workspaceSlug}/update-payment"
            : $"{portalBase}/{workspaceSlug}/update-payment/{subId}";
        var portalPath = $"{portalBase}/{workspaceSlug}/portal";
        var portalMagicLink = string.IsNullOrEmpty(magicToken)
            ? portalPath
            : $"{portalPath}?token={magicToken}";
        return (updatePaymentLink, portalMagicLink, updatePaymentLink);
    }
}

public static class MessageTemplateHydrator
{
    public const string PreviewFulfillmentUrl = "https://cloudflare.r2/download.pdf";

    public static readonly MessageTemplateContext Preview = new(
        CustomerName: "Ahmad Firdaus",
        CustomerEmail: "ahmad@example.com",
        CustomerPhone: "+60123456789",
        BusinessName: "Lazuar HQ",
        PlanName: "Founders Mastermind",
        Amount: "99.00",
        TotalPrice: "99.00",
        Currency: "MYR",
        DaysOverdue: "3",
        CurrentPeriodEnd: "31 Dec 2026",
        RenewalLink: "https://portal.lazuar.com/acme/update-payment/11111111-1111-1111-1111-111111111111",
        PortalMagicLink: "https://portal.lazuar.com/acme/portal?token=test_token",
        UpdatePaymentLink: "https://portal.lazuar.com/acme/update-payment/11111111-1111-1111-1111-111111111111");

    public static string Populate(string? text, MessageTemplateContext ctx)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";

        return text
            .Replace("{{customer_name}}", ctx.CustomerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{customer_email}}", ctx.CustomerEmail, StringComparison.OrdinalIgnoreCase)
            .Replace("{{customer_phone}}", ctx.CustomerPhone, StringComparison.OrdinalIgnoreCase)
            .Replace("{{business_name}}", ctx.BusinessName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{plan_name}}", ctx.PlanName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{amount}}", ctx.Amount, StringComparison.OrdinalIgnoreCase)
            .Replace("{{total_price}}", ctx.TotalPrice, StringComparison.OrdinalIgnoreCase)
            .Replace("{{currency}}", ctx.Currency, StringComparison.OrdinalIgnoreCase)
            .Replace("{{days_overdue}}", ctx.DaysOverdue, StringComparison.OrdinalIgnoreCase)
            .Replace("{{current_period_end}}", ctx.CurrentPeriodEnd, StringComparison.OrdinalIgnoreCase)
            .Replace("{{renewal_link}}", ctx.RenewalLink, StringComparison.OrdinalIgnoreCase)
            .Replace("{{portal_magic_link}}", ctx.PortalMagicLink, StringComparison.OrdinalIgnoreCase)
            .Replace("{{update_payment_link}}", ctx.UpdatePaymentLink, StringComparison.OrdinalIgnoreCase);
    }

    public static string PopulatePreview(string? text)
    {
        var populated = Populate(text, Preview);
        if (string.IsNullOrEmpty(populated)) return populated;
        return populated.Replace("{{fulfillment_url}}", PreviewFulfillmentUrl, StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatMoney(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static string FormatMoney(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? FormatMoney(amount)
            : "";
    }

    public static string FormatPeriodEnd(DateTime? value)
    {
        if (value is null) return "";
        var utc = value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();
        return utc.ToString("d MMM yyyy", CultureInfo.GetCultureInfo("en-GB"));
    }

    public static string FormatPeriodEnd(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return DateTime.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? FormatPeriodEnd(parsed)
            : "";
    }
}
