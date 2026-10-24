using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace MacKeys;

public static class ProfileDocuments
{
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public static string Daily(string? existingJson = null) => Build(includeCapsLock: true, includeShortcuts: true, existingJson);

    public static string Game() => Build(includeCapsLock: true, includeShortcuts: false, existingJson: null);

    public static string Off() => Build(includeCapsLock: false, includeShortcuts: false, existingJson: null);

    private static string Build(bool includeCapsLock, bool includeShortcuts, string? existingJson)
    {
        var existing = includeShortcuts ? ParseExisting(existingJson) : null;
        var catalogOriginals = new HashSet<string>(
            RemapCatalog.Shortcuts.Select(shortcut => shortcut.OriginalKeys),
            StringComparer.Ordinal);

        var keys = new JsonArray();
        if (includeCapsLock)
        {
            keys.Add(new JsonObject
            {
                ["originalKeys"] = RemapCatalog.CapsLockCode,
                ["newRemapKeys"] = RemapCatalog.HangulCode,
            });
        }

        AppendCopies(
            keys,
            existing?["remapKeys"]?["inProcess"],
            item => ItemString(item, "originalKeys") is { } original && original != RemapCatalog.CapsLockCode);

        var global = new JsonArray();
        if (includeShortcuts)
        {
            foreach (var shortcut in RemapCatalog.Shortcuts)
            {
                global.Add(new JsonObject
                {
                    ["originalKeys"] = shortcut.OriginalKeys,
                    ["exactMatch"] = false,
                    ["operationType"] = 0,
                    ["newRemapKeys"] = shortcut.NewRemapKeys,
                });
            }
        }

        AppendCopies(
            global,
            existing?["remapShortcuts"]?["global"],
            item => ItemString(item, "originalKeys") is { } original && !catalogOriginals.Contains(original));

        var appSpecific = new JsonArray();
        AppendCopies(appSpecific, existing?["remapShortcuts"]?["appSpecific"]);

        var textKeys = new JsonArray();
        var textGlobal = new JsonArray();
        var textAppSpecific = new JsonArray();
        AppendCopies(textKeys, existing?["remapKeysToText"]?["inProcess"]);
        AppendCopies(textGlobal, existing?["remapShortcutsToText"]?["global"]);
        AppendCopies(textAppSpecific, existing?["remapShortcutsToText"]?["appSpecific"]);

        var root = new JsonObject
        {
            ["remapKeys"] = new JsonObject { ["inProcess"] = keys },
            ["remapKeysToText"] = new JsonObject { ["inProcess"] = textKeys },
            ["remapShortcuts"] = new JsonObject
            {
                ["global"] = global,
                ["appSpecific"] = appSpecific,
            },
            ["remapShortcutsToText"] = new JsonObject
            {
                ["global"] = textGlobal,
                ["appSpecific"] = textAppSpecific,
            },
        };

        return root.ToJsonString(Indented);
    }

    private static JsonObject? ParseExisting(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        var node = JsonNode.Parse(json) ?? throw new JsonException("Keyboard Manager 프로필 JSON 이 비어 있습니다.");
        if (node is not JsonObject root)
            throw new JsonException("Keyboard Manager 프로필 JSON 의 루트가 객체가 아닙니다.");

        return root;
    }

    private static void AppendCopies(JsonArray destination, JsonNode? source, Func<JsonObject, bool>? include = null)
    {
        if (source is not JsonArray array)
            return;

        foreach (var item in array)
        {
            if (item is not JsonObject obj)
                continue;
            if (include is not null && !include(obj))
                continue;

            destination.Add(obj.DeepClone());
        }
    }

    private static string? ItemString(JsonObject item, string name)
    {
        if (item[name] is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
            return null;

        return value.GetValue<string>();
    }
}
