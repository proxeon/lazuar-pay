using System.Collections.Generic;
using System.Text.RegularExpressions;
using BuildingBlocks.Domain;

namespace Modules.One.Domain.Rules;

public class OrganizationSlugMustBeValidRule : IBusinessRule
{
    private readonly string _slug;
    private static readonly HashSet<string> ReservedSlugs = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "api", "app", "admin", "dashboard", "portal", "system",
        "www", "support", "help", "mail", "blog", "docs",
        "stripe", "billplz", "lazuar", "one", "auth", "login"
    };

    private static readonly Regex SlugRegex = new Regex(@"^[a-z0-9-]+$", RegexOptions.Compiled);

    public OrganizationSlugMustBeValidRule(string slug)
    {
        _slug = slug;
    }

    public bool IsBroken()
    {
        if (string.IsNullOrWhiteSpace(_slug))
            return true;

        if (_slug.Length < 3 || _slug.Length > 63)
            return true;

        if (!SlugRegex.IsMatch(_slug))
            return true;

        if (_slug.StartsWith('-') || _slug.EndsWith('-') || _slug.Contains("--"))
            return true;

        if (ReservedSlugs.Contains(_slug))
            return true;

        return false;
    }

    public string Message => "The provided workspace slug is invalid, improperly formatted, or reserved for system use. Slugs must be between 3 and 63 characters long, contain only lowercase letters, numbers, and single hyphens.";
}
