; Inno Setup script for Tower of Chrome.
; Build the game first (BuildScript.BuildWindows -> src/TowerOfChrome.Unity/Builds/Windows/),
; then compile this script:
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\TowerOfChrome.iss
; Output lands in installer\Output\TowerOfChrome-Setup-<version>.exe

#define MyAppName "Tower of Chrome"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "Fajitas"
#define MyAppExeName "TowerOfChrome.exe"
#define BuildDir "..\src\TowerOfChrome.Unity\Builds\Windows"

[Setup]
AppId={{9F1B7C7C-9C6C-4E2F-9C1B-5B2D4E6E6C10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppMutex={#MyAppName}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=TowerOfChrome-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
; Same AppId across versions means Inno already treats this as an upgrade (reuses the previous
; install dir, closes a running instance via AppMutex) -- the [Code] section below goes further
; and silently runs the *previous* version's uninstaller first, so files removed between versions
; don't linger instead of just being overwritten in place.

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#BuildDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\UnityPlayer.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\UnityCrashHandler64.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\TowerOfChrome_Data\*"; DestDir: "{app}\TowerOfChrome_Data"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#BuildDir}\MonoBleedingEdge\*"; DestDir: "{app}\MonoBleedingEdge"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#BuildDir}\D3D12\*"; DestDir: "{app}\D3D12"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
// Standard Inno Setup clean-upgrade pattern: detect a previous install of this same AppId via
// its uninstall registry entry, and run that old uninstaller silently before Setup proceeds --
// otherwise a version that removed/renamed files would leave the stale ones behind alongside
// the new install, since a plain file-overwrite install never deletes anything.
function GetUninstallString(): String;
var
  sUnInstPath: String;
  sUnInstallString: String;
begin
  sUnInstPath := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{9F1B7C7C-9C6C-4E2F-9C1B-5B2D4E6E6C10}_is1';
  sUnInstallString := '';
  if not RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString) then
    RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString);
  Result := sUnInstallString;
end;

function IsUpgrade(): Boolean;
begin
  Result := (GetUninstallString() <> '');
end;

procedure UninstallOldVersion();
var
  sUnInstallString: String;
  iResultCode: Integer;
begin
  sUnInstallString := GetUninstallString();
  if sUnInstallString <> '' then
  begin
    sUnInstallString := RemoveQuotes(sUnInstallString);
    Exec(sUnInstallString, '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, iResultCode);
  end;
end;

function InitializeSetup(): Boolean;
begin
  if IsUpgrade() then
    UninstallOldVersion();
  Result := True;
end;
