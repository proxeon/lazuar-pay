using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Modules.Billing.Contracts;

namespace Modules.Billing.Infrastructure.Services;

/// <summary>Strongly-typed binding for the appsettings "Credits" section.</summary>
public class CreditCostOptions
{
    public Dictionary<string, int> Costs { get; set; } = new();
    public List<CreditPackageOption> Packages { get; set; } = new();
    public int StarterGrant { get; set; }
}

public class CreditPackageOption
{
    public decimal AmountMyr { get; set; }
    public int Credits { get; set; }
}

public class CreditCostService : ICreditCostService
{
    private readonly Dictionary<CreditAction, int> _costs;
    private readonly List<CreditPackage> _packages;
    private readonly int _starterGrant;

    public CreditCostService(IOptions<CreditCostOptions> options)
    {
        var opts = options.Value;
        _costs = new Dictionary<CreditAction, int>();
        foreach (var (key, value) in opts.Costs)
        {
            if (Enum.TryParse<CreditAction>(key, true, out var action))
                _costs[action] = value;
        }

        _packages = opts.Packages
            .OrderBy(p => p.AmountMyr)
            .Select(p => new CreditPackage(p.AmountMyr, p.Credits))
            .ToList();

        _starterGrant = opts.StarterGrant;
    }

    public int GetCost(CreditAction action) => _costs.TryGetValue(action, out var cost) ? cost : 0;

    public IReadOnlyList<CreditPackage> GetPackages() => _packages.AsReadOnly();

    public int GetStarterGrant() => _starterGrant;
}
