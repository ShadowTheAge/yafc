using System.Text.Encodings.Web;
using System.Text.Json;

namespace YAFC.Model
{
    public static class JsonUtils
    {
        public static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions {Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true, IgnoreReadOnlyProperties = true};
        public static readonly JsonWriterOptions DefaultWriterOptions = new JsonWriterOptions {Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = true};
        
        public static bool ReadStartObject(this ref Utf8JsonReader reader)
        {
            var token = reader.TokenType;
            if (token == JsonTokenType.Null || token == JsonTokenType.StartObject)
            {
                reader.Read();
                return token == JsonTokenType.StartObject;
            }
            throw new JsonException("Expected object or null");
        }
        
        public static bool ReadStartArray(this ref Utf8JsonReader reader)
        {
            var token = reader.TokenType;
            if (token == JsonTokenType.Null || token == JsonTokenType.StartArray)
            {
                reader.Read();
                return token == JsonTokenType.StartArray;
            }
            throw new JsonException("Expected array or null");
        }

        public static bool ReadEndObject(this ref Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                reader.Read();
                return true;
            }
            return false;
        }
        
        public static bool ReadEndArray(this ref Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                reader.Read();
                return true;
            }
            return false;
        }

        public static string ReadString(this ref Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
                reader.Read();
            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                reader.Read();
                return str;
            }
            throw new JsonException("Expected string");
        }
    }
}