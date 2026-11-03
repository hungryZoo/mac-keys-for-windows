namespace MacKeys;

public readonly record struct ReleaseVersion(int Major, int Minor, int Patch) : IComparable<ReleaseVersion>
{
    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if (text.Length > 1 && (text[0] == 'v' || text[0] == 'V'))
            text = text[1..];

        var cut = text.IndexOfAny(['+', '-']);
        if (cut >= 0)
            text = text[..cut];

        var parts = text.Split('.');
        if (parts.Length < 1 || parts.Length > 4)
            return false;
        if (!int.TryParse(parts[0], out var major) || major < 0)
            return false;

        var minor = 0;
        var patch = 0;
        if (parts.Length > 1 && (!int.TryParse(parts[1], out minor) || minor < 0))
            return false;
        if (parts.Length > 2 && (!int.TryParse(parts[2], out patch) || patch < 0))
            return false;

        version = new ReleaseVersion(major, minor, patch);
        return true;
    }

    public int CompareTo(ReleaseVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
            return major;
        var minor = Minor.CompareTo(other.Minor);
        if (minor != 0)
            return minor;
        return Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
