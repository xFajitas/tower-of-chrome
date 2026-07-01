; Inno Setup script for Tower of Chrome.
; Build the game first (BuildScript.BuildWindows -> src/TowerOfChrome.Unity/Builds/Windows/),
; then compile this script:
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\TowerOfChrome.iss
; Output lands in installer\Output\TowerOfChrome-Setup-<version>.exe

#define MyAppName "Tower of Chrome"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Fajitas"
#define MyAppExeName "TowerOfChrome.exe"
#define BuildDir "..\src\TowerOfChrome.Unity\Builds\Windows"

[Setup]
AppId={{9F1B7C7C-9C6C-4E2F-9C1B-5B2D4E6E6C10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
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
