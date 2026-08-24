using Lazuar.Pay.Data;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.PublicPay;

internal static class CheckoutUrls
{
    public static string Success(CheckoutRow checkout, IConfiguration config, IHostEnvironment env) =>
        string.IsNullOrWhiteSpace(checkout.SuccessUrl)
            ? Base(config, env) + "/c/" + checkout.PublicToken + "?status=verifying"
            : checkout.SuccessUrl;

    public static string Cancel(CheckoutRow checkout, IConfiguration config, IHostEnvironment env) =>
        string.IsNullOrWhiteSpace(checkout.CancelUrl)
            ? Base(config, env) + "/c/" + checkout.PublicToken
            : checkout.CancelUrl;

    public static string Base(IConfiguration config, IHostEnvironment env)
    {
        var raw = config["Pay:CheckoutBaseUrl"]?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        if (env.IsEnvironment("Testing"))
        {
            return "http://localhost:5179";
        }

        throw new InvalidOperationException("Pay:CheckoutBaseUrl is required");
    }
}
