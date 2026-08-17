using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Microsoft.Extensions.Configuration;
using Modules.Billing.Contracts;
using Modules.One.Application.Queries;

namespace Modules.One.Infrastructure.Queries;

public sealed class GetPublicPricingQueryHandler : IQueryHandler<GetPublicPricingQuery, PublicPricingDto>
{
    public const double GmvTakePercent = 0;

    private readonly ICreditCostService _credits;
    private readonly IConfiguration _config;

    public GetPublicPricingQueryHandler(ICreditCostService credits, IConfiguration config)
    {
        _credits = credits;
        _config = config;
    }

    public Task<PublicPricingDto> Handle(GetPublicPricingQuery request, CancellationToken cancellationToken)
    {
        var packages = _credits.GetPackages()
            .Select(p => new PublicCreditPackageDto
            {
                Amount_myr = (double)p.AmountMyr,
                Credits = p.Credits
            })
            .ToList();

        var planCode = _config["Saas:Plan:Code"] ?? "hub_starter";
        var planName = _config["Saas:Plan:Name"] ?? "Hub Starter";
        var planAmount = ParseDecimal(_config["Saas:Plan:AmountMyr"]);
        var planInterval = _config["Saas:Plan:Interval"] ?? "mo";
        var planCurrency = _config["Saas:Plan:Currency"] ?? "MYR";
        var sstRate = ParseDecimal(_config["Saas:Seller:SstRate"]);
        var sstReason = string.IsNullOrWhiteSpace(_config["Saas:Seller:SstReason"])
            ? "Supplier not SST-registered"
            : _config["Saas:Seller:SstReason"]!;

        var sstPct = sstRate.ToString("0.##", CultureInfo.InvariantCulture);
        var sstNote =
            $"SST {sstPct}% — {sstReason}. Confirm with your accountant. We do not add SST at checkout today.";

        return Task.FromResult(new PublicPricingDto
        {
            Gmv_take_percent = GmvTakePercent,
            Starter_credits = _credits.GetStarterGrant(),
            Packages = packages,
            Sst_rate = (double)sstRate,
            Sst_note = sstNote,
            Checkout_is_free = false,
            Lhdn_credits_live = false,
            Whatsapp_credits_live = false,
            Lhdn_submit_credits = _credits.GetCost(CreditAction.LhdnSubmit),
            Whatsapp_send_credits = _credits.GetCost(CreditAction.WhatsAppSend),
            Hub_plan = new PublicHubPlanDto
            {
                Code = planCode,
                Name = planName,
                Amount_myr = (double)planAmount,
                Interval = planInterval,
                Currency = planCurrency
            }
        });
    }

    private static decimal ParseDecimal(string? raw)
    {
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return 0;
    }
}
