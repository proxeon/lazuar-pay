namespace Modules.Lhdn.Application;

public sealed class LhdnSigningOptions
{
    public const string SectionName = "Lhdn";

    /// <summary>Off (default) or Auto. Auto signs v1.1 JSON only when a decryptable .p12 is on file.</summary>
    public string Signing { get; set; } = "Off";

    public decimal B2cIndividualThresholdMyr { get; set; } = 10000m;

    public bool IsAuto => string.Equals(Signing, "Auto", StringComparison.OrdinalIgnoreCase);
}
