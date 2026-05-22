; ClinicOS USA Admin EN - local license administration

#define MyAppName "ClinicOS USA Admin"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "SoftwareOS"
#define MyAppExeName "ClinicOS_USA_Admin.exe"

[Setup]
AppId={{CLINICOS-USA-ADMIN-EN-2026-001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppComments=USA Admin local - HWID license administration.
DefaultDirName={autopf}\ClinicOS\USA\Admin
DefaultGroupName=ClinicOS USA
AllowNoIcons=yes
LicenseFile=..\..\legal_templates\hipaa\EULA_EN.txt
OutputDir=..\..\Distribucion\Installers\USA
OutputBaseFilename=ClinicOS_USA_Admin_EN_Setup
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
Source: "..\..\Distribucion\ClinicOS_USA_Admin_EN\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\Distribucion\ClinicOS_USA_Admin_EN\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\certificates\SoftwareOS_Compliance.cer"; DestDir: "{app}\certificates"; Flags: ignoreversion
Source: "..\..\legal_templates\disclaimers\HIPAA_Responsibility_Disclaimer_EN.txt"; DestDir: "{app}\legal"; Flags: ignoreversion
Source: "..\..\legal_templates\hipaa\HIPAA_Notice_EN.txt"; DestDir: "{app}\legal"; Flags: ignoreversion

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\ClinicOSUSAAdmin_is1"; ValueType: string; ValueName: "ComplianceFramework"; ValueData: "HIPAA Technical Safeguards"
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\ClinicOSUSAAdmin_is1"; ValueType: string; ValueName: "ClinicOSEdition"; ValueData: "Admin"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Messages]
WelcomeLabel2=USA ADMIN%n%nThis module administers local HWID licenses according to the active plan.%n%n[name] will be installed on your computer.
LicenseLabel=License Agreement - USA Admin
