#define MyAppName "ProFan"
#define MyAppVersion "1.1.13"
#define MyAppPublisher "Joshua Ezenwa"
#define MyAppExeName "ProFan.exe"

[Setup]
AppId={{B6DF30E5-98E6-43D7-9E9B-3E0725C5808B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ProFan
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=ProFan-Setup
SetupIconFile=..\assets\ProFan.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern dynamic
ShowLanguageDialog=yes
CloseApplications=yes
RestartApplications=no
AppMutex=ProFan-ASUS-HN7306

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "..\build\ProFan.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[INI]
Filename: "{app}\ProFan.ini"; Section: "General"; Key: "Language"; String: "{code:SelectedLanguage}"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: shellexec nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--exit"; Flags: runhidden waituntilterminated; RunOnceId: "StopProFan"

[Code]
function SelectedLanguage(Param: String): String;
begin
  if ActiveLanguage = 'spanish' then
    Result := 'es'
  else
    Result := 'en';
end;
