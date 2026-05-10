#define MyAppName "Yui VRM AI Studio"
#define MyAppVersion "0.1.0-alpha.1"
#define MyAppPublisher "Yui VRM AI Studio"
#define SourceRoot "..\..\public\YuiVRMAIStudio_Public"

[Setup]
AppId={{D0DDE4F4-BC75-4E93-9B51-7F7D51E6B7B1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\YuiVRMAIStudio
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=YuiVRMAIStudioSetup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest

[Files]
Source: "{#SourceRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Start Yui Backend + VOICEVOX"; Filename: "{app}\Start_Yui_Backend_And_VOICEVOX.bat"; WorkingDir: "{app}"
Name: "{group}\Stop Yui Services"; Filename: "{app}\Stop_Yui_Backend_And_VOICEVOX.bat"; WorkingDir: "{app}"
Name: "{group}\Setup Backend BYOK"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\setup_backend_byok.ps1"""; WorkingDir: "{app}"
Name: "{group}\BYOK Setup Guide"; Filename: "{app}\docs\PUBLIC_BYOK_SETUP.md"
Name: "{group}\Yui VRM AI Studio"; Filename: "{app}\builds\YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1\Yui VRM AI Studio.exe"; WorkingDir: "{app}\builds\YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1"

[Run]
Filename: "{app}\docs\PUBLIC_BYOK_SETUP.md"; Description: "Open BYOK setup guide"; Flags: postinstall shellexec skipifsilent unchecked
