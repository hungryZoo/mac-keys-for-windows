using System.Diagnostics;
using System.IO;
using System.Text;

namespace MacKeys;

internal static class UpdateInstaller
{
    public static void EnsureCanInstall()
    {
        var directory = InstallDirectory();
        Directory.CreateDirectory(directory);
        var probe = Path.Combine(directory, ".mackeys-update-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(probe, "");
        File.Delete(probe);
    }

    // The running exe stays locked, so this process has to exit before setup replaces it.
    // Setup itself starts the installed exe again.
    public static void ScheduleInstall(string setupPath)
    {
        if (string.IsNullOrWhiteSpace(setupPath) || !File.Exists(setupPath))
            throw new InvalidOperationException("설치 파일을 찾지 못했습니다.");
        if (setupPath.Contains('"'))
            throw new InvalidOperationException("경로에 따옴표가 있어 업데이트할 수 없습니다.");

        var scriptDirectory = Path.Combine(Path.GetTempPath(), "MacKeys", "update");
        Directory.CreateDirectory(scriptDirectory);
        var scriptPath = Path.Combine(scriptDirectory, "apply.ps1");
        File.WriteAllText(scriptPath, Script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo
        {
            FileName = powershell,
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" -ProcessId {Environment.ProcessId} -Setup \"{setupPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (Process.Start(start) == null)
            throw new InvalidOperationException("업데이트를 적용할 프로세스를 시작하지 못했습니다.");
    }

    private static string InstallDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("사용자 폴더를 찾지 못했습니다.");
        return Path.Combine(root, "MacKeys");
    }

    private const string Script = """
        param(
          [int]$ProcessId,
          [string]$Setup
        )
        $deadline = (Get-Date).AddSeconds(40)
        while ((Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) -and ((Get-Date) -lt $deadline)) {
          Start-Sleep -Milliseconds 200
        }
        if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
          Add-Type -AssemblyName PresentationFramework
          [System.Windows.MessageBox]::Show('실행 중인 프로그램이 끝나지 않아 업데이트하지 못했습니다.', 'Mac Keys for Windows')
          exit 1
        }
        $log = Join-Path $env:TEMP 'MacKeys\update\setup.log'
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $Setup
        $psi.Arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="' + $log + '"'
        $psi.UseShellExecute = $true
        $installer = [System.Diagnostics.Process]::Start($psi)
        if ($null -eq $installer) {
          Add-Type -AssemblyName PresentationFramework
          [System.Windows.MessageBox]::Show('설치 프로그램을 시작하지 못했습니다.', 'Mac Keys for Windows')
          exit 1
        }
        $installer.WaitForExit()
        if ($installer.ExitCode -ne 0) {
          Add-Type -AssemblyName PresentationFramework
          [System.Windows.MessageBox]::Show("설치에 실패했습니다. (코드 $($installer.ExitCode))`n$log", 'Mac Keys for Windows')
          exit 1
        }
        """;
}
