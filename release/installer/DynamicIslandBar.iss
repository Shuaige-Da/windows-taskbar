#ifndef AppVersion
#define AppVersion "1.0.0"
#endif

#ifndef SourceDir
#define SourceDir "..\..\artifacts\release\DynamicIslandBar-v1.0.0-win-x64\publish"
#endif

#ifndef OutputDir
#define OutputDir "..\..\artifacts\release\DynamicIslandBar-v1.0.0-win-x64\installer"
#endif

#ifndef DependencyLabel
#define DependencyLabel "framework-dependent"
#endif

[Setup]
AppId={{7E60BB32-46B6-46D2-84E5-6C8D0E8F1E40}
AppName=DynamicIslandBar
AppVersion={#AppVersion}
AppPublisher=DynamicIslandBar
AppPublisherURL=https://github.com/Shuaige-Da/windows-taskbar
AppSupportURL=https://github.com/Shuaige-Da/windows-taskbar/issues
AppUpdatesURL=https://github.com/Shuaige-Da/windows-taskbar
DefaultDirName={localappdata}\Programs\DynamicIslandBar
DefaultGroupName=DynamicIslandBar
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=DynamicIslandBar-Setup-v{#AppVersion}-win-x64-{#DependencyLabel}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\DynamicIslandBar.exe
SetupLogging=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "开机自动启动 DynamicIslandBar"; GroupDescription: "启动选项"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\DynamicIslandBar"; Filename: "{app}\DynamicIslandBar.exe"
Name: "{group}\卸载 DynamicIslandBar"; Filename: "{uninstallexe}"
Name: "{autodesktop}\DynamicIslandBar"; Filename: "{app}\DynamicIslandBar.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DynamicIslandBar"; ValueData: """{app}\DynamicIslandBar.exe"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\DynamicIslandBar.exe"; Description: "启动 DynamicIslandBar"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM DynamicIslandBar.exe /F"; Flags: runhidden; RunOnceId: "StopDynamicIslandBar"
