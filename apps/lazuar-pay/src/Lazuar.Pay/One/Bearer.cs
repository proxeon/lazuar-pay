namespace Lazuar.Pay.One;

internal static class Bearer
{
    public static bool TryGet(HttpRequest request, out string authorization)
    {
        authorization = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return false;
        }

        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return authorization.Length > prefix.Length && !string.IsNullOrWhiteSpace(authorization[prefix.Length..]);
    }
}
