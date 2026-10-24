namespace MacKeys;

public static class ProcessNames
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var trimmed = value.Trim().Trim('"');
        var file = trimmed.Contains('\\') || trimmed.Contains('/')
            ? Path.GetFileName(trimmed)
            : trimmed;

        if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            file = file[..^4];

        return file;
    }

    public static bool Equal(string? left, string? right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        return a.Length > 0 && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
