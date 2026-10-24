using System.Diagnostics;
using Microsoft.Win32;

namespace MacKeys;

public sealed record PowerToysInstallation(bool Installed, string? ExecutablePath, string Source)
{
    public bool CanOpenSettings =>
        !string.IsNullOrWhiteSpace(ExecutablePath) && File.Exists(ExecutablePath);

    public static PowerToysInstallation Missing { get; } = new(false, null, "missing");
}

public sealed record PowerToysDetectOptions(
    IReadOnlyList<string>? ProbePaths,
    bool SearchRegistry,
    bool SearchRunningProcess,
    bool AllowWinget)
{
    public static PowerToysDetectOptions Production { get; } = new(null, true, true, true);
}

public static class PowerToysLocator
{
    public const string WingetPackageId = "Microsoft.PowerToys";
    public const string WingetInstallArguments = "install --id Microsoft.PowerToys -e";
    public const string StoreUri = "ms-windows-store://pdp/?ProductId=XP89DCGQ3K6VLD";
    public const string SettingsArguments = "--open-settings=KBM";
    public const string EngineProcessName = "PowerToys.KeyboardManagerEngine";

    public static IReadOnlyList<string> DefaultProbePaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return new[]
        {
            Path.Combine(local, "PowerToys", "PowerToys.exe"),
            Path.Combine(programFiles, "PowerToys", "PowerToys.exe"),
            Path.Combine(programFilesX86, "PowerToys", "PowerToys.exe"),
        };
    }

    public static PowerToysInstallation Detect(PowerToysDetectOptions? options = null)
    {
        options ??= PowerToysDetectOptions.Production;
        foreach (var path in options.ProbePaths ?? DefaultProbePaths())
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return new PowerToysInstallation(true, Path.GetFullPath(path), "path");
        }

        if (options.SearchRunningProcess)
        {
            var running = RunningExecutable();
            if (running != null)
                return new PowerToysInstallation(true, running, "process");
        }

        if (options.SearchRegistry)
        {
            var registered = RegistryExecutable();
            if (registered != null)
                return new PowerToysInstallation(true, registered, "registry");
        }

        if (options.AllowWinget && WingetListsPackage() == true)
            return new PowerToysInstallation(true, null, "winget");

        return PowerToysInstallation.Missing;
    }

    public static bool IsEngineRunning()
    {
        var processes = Process.GetProcessesByName(EngineProcessName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    public static void StartWingetInstall()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = WingetInstallArguments,
            UseShellExecute = true,
        });
    }

    public static void OpenStore()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = StoreUri,
            UseShellExecute = true,
        });
    }

    public static void OpenKeyboardManagerSettings(string executablePath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = SettingsArguments,
            UseShellExecute = false,
        });
    }

    private static string? RunningExecutable()
    {
        var processes = Process.GetProcessesByName("PowerToys");
        try
        {
            foreach (var process in processes)
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        return path;
                }
                catch
                {
                    // A protected or elevated process can refuse MainModule.
                }
            }
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }

        return null;
    }

    private static string? RegistryExecutable()
    {
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            var found = SearchHive(hive);
            if (found != null)
                return found;
        }

        return null;
    }

    private static string? SearchHive(RegistryKey hive)
    {
        foreach (var root in new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        })
        {
            using var key = hive.OpenSubKey(root);
            if (key == null)
                continue;

            foreach (var name in key.GetSubKeyNames())
            {
                using var app = key.OpenSubKey(name);
                var displayName = app?.GetValue("DisplayName") as string;
                if (displayName == null ||
                    displayName.IndexOf("PowerToys", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var location = app?.GetValue("InstallLocation") as string;
                if (!string.IsNullOrWhiteSpace(location))
                {
                    var exe = Path.Combine(location.Trim().Trim('"'), "PowerToys.exe");
                    if (File.Exists(exe))
                        return exe;
                }

                var icon = app?.GetValue("DisplayIcon") as string;
                var iconPath = icon?.Split(',')[0].Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(iconPath) &&
                    iconPath.EndsWith("PowerToys.exe", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(iconPath))
                {
                    return iconPath;
                }
            }
        }

        return null;
    }

    private static bool? WingetListsPackage()
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = "list --id Microsoft.PowerToys -e --accept-source-agreements --disable-interactivity",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(start);
            if (process == null)
                return null;

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(20000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // The lookup is only a hint. A stuck winget does not change the files.
                }

                return null;
            }

            var text = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
            return process.ExitCode == 0 &&
                text.Contains(WingetPackageId, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }
}
