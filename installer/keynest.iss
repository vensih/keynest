; Keynest Installer
; Requires Inno Setup 6.3+  https://jrsoftware.org/isinfo.php

#define AppName      "Keynest"
#define AppVersion   "0.2.1-alpha"
#define AppPublisher "Vensi"
#define AppExeName   "Keynest.Windows.exe"
#define SourceDir    "..\Keynest.Windows\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"
#define PrereqDir    "prereqs"

[Setup]
; Stable GUID — do NOT change between releases (enables in-place upgrades)
AppId={{D4B8A3E1-7C5F-4E2B-9A6D-3F8C2E5B7D0A}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/vensi/keynest
AppSupportURL=https://github.com/vensi/keynest/issues
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Per-user install — no UAC prompt needed
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=output
OutputBaseFilename=keynest-setup-{#AppVersion}
SetupIconFile=..\Keynest.Windows\Assets\appicon.ico
WizardSmallImageFile=..\Keynest.Windows\Assets\appicon.png
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; x64 only — WinUI 3 / Windows App SDK requirement
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
; Windows 10 1809 minimum (Windows App SDK 2.x requirement)
MinVersion=10.0.17763
; Uninstaller
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
; Allow upgrading without uninstalling first
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; \
  Description: "Create a &desktop shortcut"; \
  GroupDescription: "Additional icons:"; \
  Flags: unchecked

[Files]
; App files
Source: "{#SourceDir}\*"; \
  DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

; Prereqs — extracted to temp, run before app files are placed
Source: "{#PrereqDir}\VC_redist.x64.exe"; \
  DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "{#PrereqDir}\WindowsAppRuntimeInstall-x64.exe"; \
  DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";     Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; 1. VC++ 2022 — silent, skip if already installed (/norestart suppresses reboot prompt)
Filename: "{tmp}\VC_redist.x64.exe"; \
  Parameters: "/install /quiet /norestart"; \
  StatusMsg: "Installing Visual C++ Runtime..."; \
  Flags: waituntilterminated

; 2. Windows App SDK 2.0 runtime — quiet, no restart
Filename: "{tmp}\WindowsAppRuntimeInstall-x64.exe"; \
  Parameters: "--quiet"; \
  StatusMsg: "Installing Windows App Runtime..."; \
  Flags: waituntilterminated

; 3. Launch app after install (optional checkbox)
Filename: "{app}\{#AppExeName}"; \
  Description: "Launch {#AppName}"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Vault data (%APPDATA%\Keynest) is intentionally left behind on uninstall.
Type: dirifempty; Name: "{app}"
