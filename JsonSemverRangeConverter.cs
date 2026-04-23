using System.Text.Json;
using System.Text.Json.Serialization;
using Semver;

namespace Olib.Modding
{
    public class JsonSemverRangeConverter : JsonConverter<SemVersionRange>
    {
        public override SemVersionRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            if (str == null)
                throw new JsonException("Expected a string value for SemVersion.");

            if (SemVersionRange.TryParse(str, SemVersionRangeOptions.Strict, out var version))
                return version;

            throw new JsonException($"Invalid SemVersion format: '{str}'.");
        }

        public override void Write(Utf8JsonWriter writer, SemVersionRange value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
