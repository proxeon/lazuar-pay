namespace Lazuar.Pay.Gateways;

public static class BuyerEmail
{
    public const string Placeholder = "customer@example.com";

    public static bool IsUsable(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && !string.Equals(email.Trim(), Placeholder, StringComparison.OrdinalIgnoreCase);

    public static string NameFrom(string? email, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return "Customer";
        }

        var at = email.IndexOf('@');
        return at > 0 ? email[..at] : "Customer";
    }
}
