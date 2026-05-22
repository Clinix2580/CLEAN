; ClinicOS USA Demo EN - read-only local demo

#define MyAppName "ClinicOS USA Demo"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "SoftwareOS"
#define MyAppExeName "ClinicOS_USA_Demo.exe"

[Setup]
AppId={{CLINICOS-USA-DEMO-EN-2026-001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppComments=USA local demo - Read-Only Mode.
DefaultDirName={autopf}\ClinicOS\USA\Demo
DefaultGroupName=ClinicOS USA
AllowNoIcons=yes
LicenseFile=..\..\legal_templates\hipaa\EULA_EN.txt
OutputDir=..\..\Distribucion\Installers\USA
OutputBaseFilename=ClinicOS_USA_Demo_EN_Setup
SetupIconFile=..\..\ClinicOS.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
DisableDirPage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\..\Distribucion\ClinicOS_USA_Demo_EN\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\Distribucion\ClinicOS_USA_Demo_EN\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\legal_templates\disclaimers\HIPAA_Responsibility_Disclaimer_EN.txt"; DestDir: "{app}\legal"; Flags: ignoreversion
Source: "..\..\legal_templates\hipaa\HIPAA_Notice_EN.txt"; DestDir: "{app}\legal"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Messages]
WelcomeLabel2=USA DEMO - READ ONLY%n%nThis demo uses fictional data and local technical safeguards for HIPAA-regulated environments.%n%n[name] will be installed on your computer.
LicenseLabel=License Agreement - USA Demo
