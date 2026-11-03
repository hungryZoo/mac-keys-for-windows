using System.Diagnostics;
using System.IO;
using System.Text;

namespace MacKeys;

internal static class UpdateInstaller
{
    public static void EnsureCanReplace(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrEmpty(directory))
            throw new InvalidOperationException("실행 파일이 있는 폴더를 찾지 못했습니다.");

        var probe = Path.Combine(directory, ".mackeys-update-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(probe, "");
        File.Delete(probe);
    }

    // The running file stays locked, so a second process waits, copies, and starts it again.
    public static void ScheduleReplace(string targetPath, string downloadedPath)
    {
        if (targetPath.Contains('"') || downloadedPath.Contains('"'))
            throw new InvalidOperationException("경로에 따옴표가 있어 업데이트할 수 없습니다.");

        var scriptDirectory = Path.Combine(Path.GetTempPath(), "MacKeys", "update");
        Directory.CreateDirectory(scriptDirectory);
        var scriptPath = Path.Combine(scriptDirectory, "apply.ps1");
        File.WriteAllText(scriptPath, Script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo
        {
            FileName = powershell,
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" -ProcessId {Environment.ProcessId} -Source \"{downloadedPath}\" -Target \"{targetPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (Process.Start(start) == null)
            throw new InvalidOperationException("업데이트를 적용할 프로세스를 시작하지 못했습니다.");
    }

    private const string Script = """
        param(
          [int]$ProcessId,
          [string]$Source,
          [string]$Target
        )
        $deadline = (Get-Date).AddSeconds(40)
        while ((Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) -and ((Get-Date) -lt $deadline)) {
          Start-Sleep -Milliseconds 200
        }
        $copied = $false
        for ($try = 0; $try -lt 20 -and -not $copied; $try++) {
          try {
            Copy-Item -LiteralPath $Source -Destination $Target -Force
            $copied = $true
          } catch {
            Start-Sleep -Milliseconds 500
          }
        }
        if (-not $copied) {
          Add-Type -AssemblyName PresentationFramework
          [System.Windows.MessageBox]::Show('새 실행 파일로 바꾸지 못했습니다.', 'Mac Keys for Windows')
          exit 1
        }
        Start-Process -FilePath $Target
        """;
}
