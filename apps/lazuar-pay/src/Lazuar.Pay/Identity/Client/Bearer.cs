using Lazuar.Pay.Hosting;

namespace Lazuar.Pay.Identity.Client;

internal static class Bearer
{
    const string Prefix = "Bearer ";

    public static bool TryGet(HttpRequest request, out string authorization)
    {
        authorization = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return false;
        }

        if (!authorization.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return authorization.Length > Prefix.Length && !string.IsNullOrWhiteSpace(authorization[Prefix.Length..]);
    }

    public static string Token(string authorization) =>
        authorization.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[Prefix.Length..].Trim()
            : authorization.Trim();

    /// <summary>One machine key. Not a JWT, not Stripe/Hub <c>sk_</c>.</summary>
    public static bool IsMachineKey(string authorization) =>
        Token(authorization).StartsWith("lzr_sk_", StringComparison.Ordinal);

    /// <summary>
    /// Stripe/Hub <c>sk_</c> as Pay Authorization is the wrong family.
    /// Vault PUT still accepts <c>sk_test</c> in JSON. Zitadel PAT shapes are
    /// not inventoried; they fail at One if they are not <c>lzr_sk_</c> or JWT.
    /// </summary>
    public static IResult? RejectWrongFamily(string authorization)
    {
        var token = Token(authorization);
        if (token.StartsWith("sk_", StringComparison.Ordinal)
            || token.StartsWith("sk_test_", StringComparison.Ordinal)
            || token.StartsWith("sk_live_", StringComparison.Ordinal))
        {
            return PayErrors.Status(401, "Unauthorized", "Invalid bearer");
        }

        return null;
    }
}
