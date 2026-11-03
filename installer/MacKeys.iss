; 버전은 src/MacKeys.App/MacKeys.App.csproj 의 Version 과 같게 유지한다.
; 릴리즈에는 MacKeys-Setup-버전.exe 와 함께 MacKeys-Setup.exe 를 올린다.
; 리드미의 latest/download/MacKeys-Setup.exe 가 그 고정 이름을 본다.
; AppId 는 한 번 정하면 바꾸지 않는다. 바꾸면 설정 앱이 다른 프로그램으로 본다.

#define MyAppName "Mac Keys for Windows"
#define MyAppVersion "1.0.4"
#define MyAppExeName "MacKeys.exe"

[Setup]
AppId={{0D6C9390-3F49-4DFD-81A9-10B7CD28ED0D}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
; 설정 > 앱에 보이는 버전은 AppVersion 이다. 설치 파일 속성만 네 칸이다.
VersionInfoVersion=1.0.4.0
VersionInfoProductVersion=1.0.4.0
VersionInfoProductName={#MyAppName}
DefaultDirName={localappdata}\MacKeys
UsePreviousAppDir=no
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=MacKeys-Setup-{#MyAppVersion}
SetupIconFile=..\src\MacKeys.App\Assets\mac-keys.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=yes
RestartApplications=no
; 창을 닫으면 트레이로 숨는다. AppMutex 를 쓰면 조용한 설치가 실행 중이라는 이유로 바로 끊긴다.

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Files]
Source: "..\src\MacKeys.App\bin\Release\net8.0-windows\win-x64\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#MyAppExeName}"; Flags: nowait skipifnotsilent

[Code]
function CloseMacKeys(): Boolean;
var
  ResultCode: Integer;
  I: Integer;
  Command: String;
begin
  if not CheckForMutexes('Local\MacKeys.SingleInstance') then
  begin
    Result := True;
    Exit;
  end;

  Command := '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command "' +
    'try { $e = [System.Threading.EventWaitHandle]::OpenExisting(''Local\MacKeys.Exit''); ' +
    '[void]$e.Set(); $e.Dispose(); exit 0 } catch { exit 2 }"';
  if (not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Command, '', SW_HIDE, ewWaitUntilTerminated, ResultCode)) or (ResultCode <> 0) then
  begin
    Result := False;
    Exit;
  end;

  for I := 1 to 50 do
  begin
    if not CheckForMutexes('Local\MacKeys.SingleInstance') then
    begin
      Result := True;
      Exit;
    end;
    Sleep(200);
  end;
  Result := False;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  CloseMacKeys();
  Result := '';
end;

procedure PointStartupAtInstall();
var
  Value: String;
  Quoted: String;
begin
  if not RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'MacKeys', Value) then
    Exit;
  if Trim(Value) = '' then
    Exit;
  Quoted := '"' + ExpandConstant('{app}\{#MyAppExeName}') + '"';
  RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'MacKeys', Quoted);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    PointStartupAtInstall();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep <> usUninstall then
    Exit;

  if not CloseMacKeys() then
    MsgBox('Mac Keys for Windows가 아직 실행 중입니다. 트레이 아이콘에서 종료한 뒤 다시 제거해 주세요.', mbError, MB_OK);

  RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'MacKeys');
end;
