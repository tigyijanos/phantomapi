using System.Text.Json;

static class JsonSchemaUtilities
{
    public static bool LooksLikeJsonSchema(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return element.TryGetProperty("$schema", out _)
            || element.TryGetProperty("type", out _)
            || element.TryGetProperty("properties", out _)
            || element.TryGetProperty("items", out _)
            || element.TryGetProperty("required", out _)
            || element.TryGetProperty("$defs", out _)
            || element.TryGetProperty("definitions", out _);
    }

    public static string NormalizeToSchemaJson(JsonElement element)
    {
        return LooksLikeJsonSchema(element)
            ? element.GetRawText()
            : JsonSerializer.Serialize(BuildJsonSchemaFromExample(element));
    }

    public static object BuildJsonSchemaFromExample(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => BuildObjectSchema(element),
            JsonValueKind.Array => BuildArraySchema(element),
            JsonValueKind.String => new Dictionary<string, object?> { ["type"] = "string" },
            JsonValueKind.Number => new Dictionary<string, object?> { ["type"] = element.TryGetInt64(out _) ? "integer" : "number" },
            JsonValueKind.True or JsonValueKind.False => new Dictionary<string, object?> { ["type"] = "boolean" },
            JsonValueKind.Null => new Dictionary<string, object?> { ["type"] = "null" },
            _ => new Dictionary<string, object?>()
        };
    }

    private static Dictionary<string, object?> BuildObjectSchema(JsonElement element)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        var required = new List<string>();
        foreach (var property in element.EnumerateObject())
        {
            properties[property.Name] = BuildJsonSchemaFromExample(property.Value);
            required.Add(property.Name);
        }

        return new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };
    }

    private static Dictionary<string, object?> BuildArraySchema(JsonElement element)
    {
        object itemsSchema;
        if (element.GetArrayLength() == 0)
        {
            itemsSchema = new Dictionary<string, object?>();
        }
        else
        {
            itemsSchema = BuildJsonSchemaFromExample(element.EnumerateArray().First());
        }

        return new Dictionary<string, object?>
        {
            ["type"] = "array",
            ["items"] = itemsSchema
        };
    }
}
