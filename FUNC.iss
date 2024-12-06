; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "FUNC"
#define MyAppVersion "2.0.1"
#define MyAppPublisher "Galaxy Pay, LLC"
#define MyAppPublisherURL "https://galaxy-pay.com"
#define MyPublishPath "publish"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{5A7F3586-4D20-473C-8D0D-851B2B6E20DE}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppPublisherURL}
;AppSupportURL={#MyAppURL}
;AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableDirPage=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Uncomment the following line to run in non administrative install mode (install for current user only.)
;PrivilegesRequired=lowest
OutputBaseFilename=func_{#MyAppVersion}_windows
SetupIconFile={#MyPublishPath}\node.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#MyPublishPath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\FUNC"; Filename: "http://localhost:3536/"; IconFilename: "{app}\node.ico"
Name: "{commondesktop}\FUNC"; Filename: "http://localhost:3536/"; IconFilename: "{app}\node.ico"

[Run]
; Kill old services if installed
Filename: "sc.exe"; Parameters: "stop AvmWinNode"
Filename: "sc.exe"; Parameters: "delete AvmWinNode"

Filename: "sc.exe"; Parameters: "create FUNC binPath= ""{app}\FUNC.exe"" start= auto"
Filename: "sc.exe"; Parameters: "start FUNC"

; Migrate old data
Filename: "cmd.exe"; Parameters: "/c ren {commonappdata}\AvmWinNode FUNC"
Filename: "cmd.exe"; Parameters: "/c md {commonappdata}\FUNC\bin"
Filename: "cmd.exe"; Parameters: "/c move {commonappdata}\FUNC\* {commonappdata}\FUNC\bin"

Filename: "http://localhost:3536/"; Flags: shellexec postinstall; Description: "Launch FUNC"

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop FUNC"; RunOnceId: "StopService"
Filename: "sc.exe"; Parameters: "delete FUNC"; RunOnceId: "DelService"
