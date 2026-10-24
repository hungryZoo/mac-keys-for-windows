using Microsoft.Win32;

namespace MacKeys;

public sealed class StartupRegistration
{
    public const string ValueName = "MacKeys";
    public const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _subKey;

    public StartupRegistration(string? subKey = null)
    {
        _subKey = string.IsNullOrWhiteSpace(subKey) ? RunSubKey : subKey;
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_subKey, writable: false);
        return key?.GetValue(ValueName) is string text && !string.IsNullOrWhiteSpace(text);
    }

    public void Enable(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new ArgumentException("실행 파일 경로가 비어 있습니다.", nameof(executablePath));
        if (executablePath.Contains('"'))
            throw new ArgumentException("실행 파일 경로에 따옴표가 있습니다.", nameof(executablePath));

        using var key = Registry.CurrentUser.CreateSubKey(_subKey);
        key.SetValue(ValueName, Quote(executablePath));
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_subKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static string Quote(string executablePath) => $"\"{executablePath}\"";

    public static string CurrentExecutable()
    {
        return Environment.ProcessPath
            ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("실행 파일 경로를 찾지 못했습니다.");
    }
}
