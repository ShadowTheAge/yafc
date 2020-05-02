using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YAFC.Model
{
    public class FactorioObjectConverter : JsonConverter<FactorioObject>
    {
        public override FactorioObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (!reader.ReadStartObject())
                return null;
            string type = null, name = null;
            while (!reader.ReadEndObject())
            {
                if (reader.ValueTextEquals("type"))
                    type = reader.ReadString();
                else if (reader.ValueTextEquals("name"))
                    name = reader.ReadString();
                else reader.Skip();
            }

            if (type != null && name != null)
            {
                if (Database.objectsByTypeName.TryGetValue((type, name), out var obj))
                    return obj;
                return null;
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, FactorioObject value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            } 
            writer.WriteStartObject();
            writer.WriteString("type", value.type);
            writer.WriteString("name", value.name);
            writer.WriteEndObject();
            
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(FactorioObject).IsAssignableFrom(typeToConvert);
        }
    }
}