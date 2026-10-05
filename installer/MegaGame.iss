; MegaGame installer script (Inno Setup 6)
;
; Compiled on a Windows runner by the windows-installer CI job:
;   ISCC /DPublishDir=<abs> /DOutputDirOverride=<abs> /DMyAppVersion=<tag>
;     installer\MegaGame.iss
;
; Both paths are injected as absolute values so nothing depends on the
; compiler's working directory.

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef PublishDir
  #error PublishDir must be defined by the caller
#endif
#ifndef OutputDirOverride
  #define OutputDirOverride "dist"
#endif

#define AppName "MegaGame"
#define AppExeName "MegaGame.exe"

[Setup]
AppId={{7C4E9B21-3F0D-4A6E-9C1B-8D2F5A6E7B34}
AppName={#AppName}
AppVersion={#MyAppVersion}
AppVerName={#AppName} {#MyAppVersion}
AppPublisher=MegaGame Studios
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDirOverride}
OutputBaseFilename=MegaGame-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Per-user install by default so no admin rights are required. This must be
; "lowest" (not "admin") or Inno warns about mixing HKCU entries into an
; elevated install.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart";   Description: "Start {#AppName} when Windows starts"; GroupDescription: "Startup"; Flags: unchecked

[Files]
; Absolute path supplied by CI. Forward slashes are deliberate: ISPP treats a
; backslash inside an injected /D value as an escape character.
Source: "{#PublishDir}/*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";        Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\{#AppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\{#AppName}"; ValueType: dword; ValueName: "RunAtStartup"; ValueData: 0; Tasks: autostart; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; Tasks: autostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Saves live in %APPDATA% so they survive an uninstall/reinstall cycle.
Type: filesandordirs; Name: "{app}\saves"