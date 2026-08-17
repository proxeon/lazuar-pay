namespace Modules.Lhdn.Domain;

/// <summary>Buyer identity rules shared by submit, TIN validation, and UBL mapping.</summary>
public static class MyInvoisBuyerRules
{
    public const string GeneralPublicTin = "EI00000000010";
    public const string StubBuyerTin = "C1234567890";
    public const string GeneralPublicIgTin = "IG1234567890";

    public static bool IsGeneralPublic(string? tin, string? idValue)
    {
        var tinOk = string.Equals(tin?.Trim(), GeneralPublicTin, StringComparison.OrdinalIgnoreCase);
        var id = idValue?.Trim();
        return tinOk && (string.IsNullOrWhiteSpace(id) || string.Equals(id, "NA", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsStubTin(string? tin)
    {
        var value = tin?.Trim();
        return string.Equals(value, StubBuyerTin, StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, GeneralPublicIgTin, StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, GeneralPublicTin, StringComparison.OrdinalIgnoreCase);
    }

    public static bool RequiresTinValidation(string documentTypeCode, string? tin, string? idValue)
    {
        if (!string.Equals(documentTypeCode, "01", StringComparison.Ordinal))
        {
            return false;
        }

        return !IsGeneralPublic(tin, idValue);
    }

    public static string DetectSubmissionFormat(string content)
    {
        var trimmed = content.AsSpan().TrimStart();
        return trimmed.Length > 0 && trimmed[0] == '{' ? "JSON" : "XML";
    }
}
