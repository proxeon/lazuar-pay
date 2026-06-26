using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.Community.Application.Queries;

public record RenderTemplatePreviewQuery(string Subject, string EmailBody, string WhatsAppBody) : IQuery<TemplatePreviewResponseDto>;

public class RenderTemplatePreviewQueryHandler : IQueryHandler<RenderTemplatePreviewQuery, TemplatePreviewResponseDto>
{
    private static readonly Dictionary<string, string> MockData = new(StringComparer.OrdinalIgnoreCase)
    {
        ["customer_name"] = "Ahmad Firdaus",
        ["customer_email"] = "ahmad.firdaus@example.com",
        ["customer_phone"] = "+60 12-345 6789",
        ["business_name"] = "Lazuar HQ",
        ["plan_name"] = "Founders Mastermind",
        ["plan_price"] = "99.00",
        ["total_price"] = "99.00",
        ["group_link"] = "https://t.me/joinchat/example",
        ["meeting_link"] = "https://zoom.us/j/123456789",
        ["renewal_link"] = "https://community.lazuar.com/checkout",
        ["current_period_end"] = "31 Dec 2026",
        ["item_name"] = "Digital Course Bundle",
        ["checkout_url"] = "https://lazuar.com/cart"
    };

    public Task<TemplatePreviewResponseDto> Handle(RenderTemplatePreviewQuery request, CancellationToken ct)
    {
        var subjectContent = MarkdownParser.ToPlainText(RenderWithMockData(request.Subject));
        var htmlEmailContent = MarkdownParser.ToHtml(RenderWithMockData(request.EmailBody));
        var textWhatsappContent = MarkdownParser.ToPlainText(RenderWithMockData(request.WhatsAppBody));

        var response = new TemplatePreviewResponseDto
        {
            Html_email_preview = htmlEmailContent,
            Text_whatsapp_preview = textWhatsappContent,
            Subject_content = subjectContent
        };

        return Task.FromResult(response);
    }

    private string RenderWithMockData(string template)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        
        var result = template;
        foreach (var kvp in MockData)
        {
            result = result.Replace($"{{{{{kvp.Key}}}}}", kvp.Value, StringComparison.OrdinalIgnoreCase);
        }
        
        return result;
    }
}
