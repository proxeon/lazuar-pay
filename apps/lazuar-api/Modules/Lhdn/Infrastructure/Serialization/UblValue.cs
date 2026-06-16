using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modules.Lhdn.Infrastructure.Serialization;

/// <summary>
/// A generic wrapper that ensures simple properties are serialized as an array containing an object with an underscore key.
/// E.g., UblValue<string>("123") outputs [{"_": "123"}] to strictly match LHDN's proprietary JSON schema.
/// </summary>
public readonly record struct UblValue<T>(T Value)
{
    public static implicit operator UblValue<T>(T value) => new(value);
}

public class UblValueConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(UblValue<>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(UblValueConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private class UblValueConverter<T> : JsonConverter<UblValue<T>>
    {
        public override UblValue<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException("Deserialization from LHDN format is not required.");
        }

        public override void Write(Utf8JsonWriter writer, UblValue<T> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WritePropertyName("_");
            JsonSerializer.Serialize(writer, value.Value, options);
            writer.WriteEndObject();
            writer.WriteEndArray();
        }
    }
}
