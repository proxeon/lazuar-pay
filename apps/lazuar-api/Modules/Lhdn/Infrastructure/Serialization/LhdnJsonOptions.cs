using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modules.Lhdn.Infrastructure.Serialization;

/// <summary>
/// Static, immutable options exclusively for LHDN payload generation.
/// Guarantees strict escaping rules and avoids interfering with global API formatting.
/// </summary>
public static class LhdnJsonOptions
{
    public static readonly JsonSerializerOptions Instance = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null,
        WriteIndented = false,
        Converters =
        {
            new UblValueConverterFactory(),
            new DecimalJsonConverter()
        }
    };
}
