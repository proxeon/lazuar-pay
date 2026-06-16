using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modules.Lhdn.Infrastructure.Serialization;

/// <summary>
/// Strips trailing zeros from decimals to match Node.js JSON.stringify() output.
/// Native .NET preserves decimal precision (100.00m -> "100.00"), which alters payload byte length and invalidates hashes.
/// </summary>
public class DecimalJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetDecimal();
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteRawValue(value.ToString("G29", CultureInfo.InvariantCulture));
    }
}
