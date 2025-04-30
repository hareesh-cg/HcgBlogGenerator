using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace HcgBlogGenerator.Core.Services;

/// <summary>
/// YamlDotNet Type Converter for DateTime
/// Necessary because YamlDotNet doesn't handle DateTime out-of-the-box perfectly sometimes
/// </summary>
public class DateTimeYamlConverter : IYamlTypeConverter {
    public bool Accepts(Type type) => type == typeof(DateTime);

    public object ReadYaml(IParser parser, Type type) {
        var scalar = parser.Consume<YamlDotNet.Core.Events.Scalar>();
        if (DateTime.TryParse(scalar.Value, out var dto)) {
            return dto;
        }
        // Attempt fallback parsing if direct parse fails (e.g., only date provided)
        if (DateTimeOffset.TryParse(scalar.Value, out var dt)) {
            // Assume local time if only date/time is provided, or UTC if Z is present?
            // This behavior might need refinement based on expected input formats.
            // Let's default to treating it as unspecified -> local offset.
            return dt.DateTime;
        }
        // Could log a warning here if parsing fails entirely
        throw new YamlException(scalar.Start, scalar.End, $"Failed to parse '{scalar.Value}' as DateTime.");
    }

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer) {
        return ReadYaml(parser, type);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type) {
        // Serialization not typically needed for parsing, but implement if required
        if (value is DateTime dto) {
            // Format using ISO 8601 standard (common in YAML)
            emitter.Emit(new YamlDotNet.Core.Events.Scalar(dto.ToString("o")));
        }
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) {
        WriteYaml(emitter, value, type);
    }
}
