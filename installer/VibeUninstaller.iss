; Inno Setup script for the VibeUninstaller setup executable.
; Built by .github/workflows/release.yml after the self-contained publish.
;   ISCC.exe /DAppVersion=0.2.0 installer\VibeUninstaller.iss

#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif

#define AppName "VibeUninstaller"
#define AppPublisher "VibeUninstaller contributors"
#define AppUrl "https://github.com/Aws-Nassar/Uninstaller_VibeCoding"

[Setup]
AppId={{8E1F2C94-5B7A-4E63-9E2B-1D4A9F0C3B57}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
LicenseFile=..\LICENSE
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.19041
OutputDir=..\artifacts\installer
OutputBaseFilename={#AppName}-{#AppVersion}-setup
SetupIconFile=..\assets\branding\app.ico
UninstallDisplayIcon={app}\VibeUninstaller.exe
UninstallDisplayName={#AppName} {#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "..\artifacts\gui\VibeUninstaller.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Explicit Uninstall folders make the shortcuts show up in the normal places.
Name: "{autoprograms}\VibeUninstaller"; Filename: "{app}\VibeUninstaller.exe"; IconFilename: "{app}\VibeUninstaller.exe"
Name: "{autodesktop}\VibeUninstaller"; Filename: "{app}\VibeUninstaller.exe"; IconFilename: "{app}\VibeUninstaller.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\VibeUninstaller.exe"; Description: "{cm:LaunchProgram,VibeUninstaller}"; Flags: nowait postinstall skipifsilent
