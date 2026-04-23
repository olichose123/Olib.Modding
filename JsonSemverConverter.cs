using System.Text.Json;
using System.Text.Json.Serialization;
using Semver;

namespace Olib.Modding
{
    public class JsonSemverConverter : JsonConverter<SemVersion>
    {
        public override SemVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            if (str == null)
                throw new JsonException("Expected a string value for SemVersion.");

            if (SemVersion.TryParse(str, SemVersionStyles.Strict, out var version))
                return version;

            throw new JsonException($"Invalid SemVersion format: '{str}'.");
        }

        public override void Write(Utf8JsonWriter writer, SemVersion value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
