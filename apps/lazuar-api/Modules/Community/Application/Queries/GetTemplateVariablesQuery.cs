// apps/lazuar-api/Modules/Community/Application/Queries/GetTemplateVariablesQuery.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.Community.Application.Queries;

public record GetTemplateVariablesQuery : IQuery<IEnumerable<TemplateVariableCategoryDto>>;

public class GetTemplateVariablesQueryHandler : IQueryHandler<GetTemplateVariablesQuery, IEnumerable<TemplateVariableCategoryDto>>
{
    public Task<IEnumerable<TemplateVariableCategoryDto>> Handle(GetTemplateVariablesQuery request, CancellationToken ct)
    {
        var categories = new List<TemplateVariableCategoryDto>
        {
            new TemplateVariableCategoryDto
            {
                Title = "Customer Profile Context",
                Items = new List<TemplateVariableDto>
                {
                    new TemplateVariableDto { Tag = "{{customer_name}}", Description = "The full display name of the member." },
                    new TemplateVariableDto { Tag = "{{customer_email}}", Description = "The registered email address of the member." },
                    new TemplateVariableDto { Tag = "{{customer_phone}}", Description = "The phone number of the member." }
                }
            },
            new TemplateVariableCategoryDto
            {
                Title = "Billing & Subscriptions",
                Items = new List<TemplateVariableDto>
                {
                    new TemplateVariableDto { Tag = "{{plan_name}}", Description = "The subscription name (e.g. Premium Tier)." },
                    new TemplateVariableDto { Tag = "{{plan_price}}", Description = "The base cost formatted in MYR." },
                    new TemplateVariableDto { Tag = "{{total_price}}", Description = "Final charge total (factoring discounts and tax overlays)." },
                    new TemplateVariableDto { Tag = "{{renewal_link}}", Description = "Direct, secure checkout billing link." },
                    new TemplateVariableDto { Tag = "{{current_period_end}}", Description = "The date the current billing cycle ends." }
                }
            },
            new TemplateVariableCategoryDto
            {
                Title = "Community Assets",
                Items = new List<TemplateVariableDto>
                {
                    new TemplateVariableDto { Tag = "{{meeting_link}}", Description = "Zoom or private scheduling access links." },
                    new TemplateVariableDto { Tag = "{{group_link}}", Description = "Direct invitation link for Telegram or WhatsApp." }
                }
            }
        };

        return Task.FromResult<IEnumerable<TemplateVariableCategoryDto>>(categories);
    }
}
