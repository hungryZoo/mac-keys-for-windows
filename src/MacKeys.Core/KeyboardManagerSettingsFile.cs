using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace MacKeys;

public static class KeyboardManagerSettingsFile
{
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public static string Patch(string? existingJson, string activeConfiguration)
    {
        if (activeConfiguration is not (Profiles.Daily or Profiles.Game or Profiles.Off))
            throw new ArgumentOutOfRangeException(nameof(activeConfiguration));

        JsonObject root;
        if (string.IsNullOrWhiteSpace(existingJson))
        {
            root = new JsonObject
            {
                ["properties"] = new JsonObject(),
                ["name"] = "Keyboard Manager",
                ["version"] = "1",
            };
        }
        else
        {
            var parsed = JsonNode.Parse(existingJson);
            root = parsed as JsonObject
                ?? throw new InvalidDataException("Keyboard Manager settings.json 의 루트가 객체가 아닙니다.");
        }

        if (root["properties"] is not JsonObject properties)
        {
            properties = new JsonObject();
            root["properties"] = properties;
        }

        if (properties["activeConfiguration"] is not JsonObject active)
        {
            active = new JsonObject();
            properties["activeConfiguration"] = active;
        }

        active["value"] = activeConfiguration;

        if (properties["keyboardConfigurations"] is not JsonObject configurations)
        {
            configurations = new JsonObject();
            properties["keyboardConfigurations"] = configurations;
        }

        var names = new JsonArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (configurations["value"] is JsonArray existing)
        {
            foreach (var item in existing)
            {
                if (item is JsonValue value &&
                    value.TryGetValue<string>(out var text) &&
                    !string.IsNullOrWhiteSpace(text) &&
                    seen.Add(text))
                {
                    names.Add(text);
                }
            }
        }

        foreach (var required in Profiles.All)
        {
            if (seen.Add(required))
                names.Add(required);
        }

        configurations["value"] = names;

        if (root["name"] is null)
            root["name"] = "Keyboard Manager";
        if (root["version"] is null)
            root["version"] = "1";

        return root.ToJsonString(Indented);
    }
}
