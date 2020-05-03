using System.Text.Encodings.Web;
using System.Text.Json;

namespace YAFC.Model
{
    public static class JsonUtils
    {
        public static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions {Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true, IgnoreReadOnlyProperties = true};
        public static readonly JsonWriterOptions DefaultWriterOptions = new JsonWriterOptions {Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = true};
        public static bool ReadStartArray(this ref Utf8JsonReader reader)
        {
            var token = reader.TokenType;
            if (token == JsonTokenType.Null)
                return false;
            if (token == JsonTokenType.StartArray)
            {
                reader.Read();
                return true;
            }
            throw new JsonException("Expected array or null");
        }
    }
}