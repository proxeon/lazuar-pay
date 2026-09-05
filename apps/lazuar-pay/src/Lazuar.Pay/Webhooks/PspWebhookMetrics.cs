using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Lazuar.Pay.Webhooks;

/// <summary>
/// plans/031/04: parse-outcome counters make PSP contract drift visible. Every rail parser
/// encodes a hand-copied snapshot of a vendor's webhook contract; when a vendor changes
/// shape, the first symptom is a spike in ignored / verify_failed / checkout_missing /
/// amount_mismatch outcomes — previously invisible beyond bare 200/400 status lines.
///
/// One measurement per delivered event, tags {provider, outcome}:
///   ok                — event parsed, validated, and accepted for processing (fulfilled,
///                       failure recorded, late-pay booked; includes 409 charges-paused and
///                       rare internal 5xx, where the contract itself held)
///   ignored           — verified but not acted on (known lifecycle events like
///                       purchase.preauthorized AND unrecognized event types; drift shows
///                       up as the ignored ratio rising toward 1 for a provider)
///   verify_failed     — signature or a required field missing/invalid (PspVerifyException)
///   dedupe            — replay of an already-recorded event
///   checkout_missing  — verified event binds to no known checkout (binding fields changed?)
///   amount_mismatch   — verified event amount differs from the quoted checkout
///   currency_mismatch — verified event currency differs from the quoted checkout
///   secret_unavailable — the 503 family (vault secret missing/undecryptable — the WrapKey
///                       rotation class), distinct from signature noise
///
/// Alert shape (ops): verify_failed / checkout_missing / amount_mismatch rate > 0 per
/// provider, or ignored ≈ total for a provider over an hour. Solana has no inbound webhook
/// route payload (its parser always refuses), so its series is probe noise.
/// </summary>
public static class PspWebhookMetrics
{
    private static readonly Meter Meter = new("Lazuar.Pay.Webhooks");

    private static readonly Counter<long> ParseOutcome =
        Meter.CreateCounter<long>("psp_parse_outcome");

    public const string Ok = "ok";
    public const string Ignored = "ignored";
    public const string VerifyFailed = "verify_failed";
    public const string Dedupe = "dedupe";
    public const string CheckoutMissing = "checkout_missing";
    public const string AmountMismatch = "amount_mismatch";
    public const string CurrencyMismatch = "currency_mismatch";
    public const string SecretUnavailable = "secret_unavailable";

    public static void Outcome(string provider, string outcome) =>
        ParseOutcome.Add(
            1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("outcome", outcome));
}
