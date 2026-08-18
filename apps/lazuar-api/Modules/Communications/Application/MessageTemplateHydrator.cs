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
        if (!string.IsNullOrEmpty(magicToken))
        {
            updatePaymentLink += $"?token={magicToken}";
        }
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

    public static string Populate(string? text, MessageTemplateContext ctx, bool htmlEncode = false)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";

        var name = htmlEncode ? HtmlEncode(ctx.CustomerName) : ctx.CustomerName;
        var email = htmlEncode ? HtmlEncode(ctx.CustomerEmail) : ctx.CustomerEmail;
        var phone = htmlEncode ? HtmlEncode(ctx.CustomerPhone) : ctx.CustomerPhone;
        var business = htmlEncode ? HtmlEncode(ctx.BusinessName) : ctx.BusinessName;
        var plan = htmlEncode ? HtmlEncode(ctx.PlanName) : ctx.PlanName;
        var renewal = htmlEncode ? SafeHttpUrl(ctx.RenewalLink) : ctx.RenewalLink;
        var portal = htmlEncode ? SafeHttpUrl(ctx.PortalMagicLink) : ctx.PortalMagicLink;
        var update = htmlEncode ? SafeHttpUrl(ctx.UpdatePaymentLink) : ctx.UpdatePaymentLink;

        return text
            .Replace("{{customer_name}}", name, StringComparison.OrdinalIgnoreCase)
            .Replace("{{customer_email}}", email, StringComparison.OrdinalIgnoreCase)
            .Replace("{{customer_phone}}", phone, StringComparison.OrdinalIgnoreCase)
            .Replace("{{business_name}}", business, StringComparison.OrdinalIgnoreCase)
            .Replace("{{plan_name}}", plan, StringComparison.OrdinalIgnoreCase)
            .Replace("{{amount}}", ctx.Amount, StringComparison.OrdinalIgnoreCase)
            .Replace("{{total_price}}", ctx.TotalPrice, StringComparison.OrdinalIgnoreCase)
            .Replace("{{currency}}", ctx.Currency, StringComparison.OrdinalIgnoreCase)
            .Replace("{{days_overdue}}", ctx.DaysOverdue, StringComparison.OrdinalIgnoreCase)
            .Replace("{{current_period_end}}", ctx.CurrentPeriodEnd, StringComparison.OrdinalIgnoreCase)
            .Replace("{{renewal_link}}", renewal, StringComparison.OrdinalIgnoreCase)
            .Replace("{{checkout_url}}", renewal, StringComparison.OrdinalIgnoreCase)
            .Replace("{{portal_magic_link}}", portal, StringComparison.OrdinalIgnoreCase)
            .Replace("{{update_payment_link}}", update, StringComparison.OrdinalIgnoreCase);
    }

    public static string PopulateHtml(string? text, MessageTemplateContext ctx) =>
        Populate(text, ctx, htmlEncode: true);

    public static string HtmlEncode(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? "");

    public static string SafeHttpUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            return url;
        }

        return "";
    }

    public static string PopulatePreview(string? text)
    {
        var populated = Populate(text, Preview, htmlEncode: true);
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
