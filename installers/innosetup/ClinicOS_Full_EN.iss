; Inno Setup Script - ClinicOS Full EN
; USA local clinical distribution - HIPAA technical safeguards

#define MyAppName "ClinicOS"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "SoftwareOS"
#define MyAppExeName "ClinicOS.exe"
#define MyAppAssocName MyAppName + " Database"
#define MyAppAssocExt ".clinicdb"
#define MyAppAssocKey StringChange(MyAppAssocName, " ", "") + MyAppAssocExt

[Setup]
AppId={{CLINICOS-FULL-EN-2026-001}
AppName={#MyAppName} (Full - English)
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\..\legal_templates\hipaa\EULA_EN.txt
OutputDir=..\..\Distribucion\Installers
OutputBaseFilename=ClinicOS_Full_EN_Setup
SetupIconFile=..\..\assets\icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
disablewelcomepage=no
DisableDirPage=no
DisableProgramGroupPage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "installcert"; Description: "Install trust certificate"; GroupDescription: "Security:"; Flags: checked
Name: "firewallexception"; Description: "{cm:CreateFirewallException}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\..\Distribucion\ClinicOS_Full_EN\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\Distribucion\ClinicOS_Full_EN\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\certificates\SoftwareOS_Compliance.cer"; DestDir: "{app}\certificates"; Flags: ignoreversion
Source: "..\..\certificates\SoftwareOS_Compliance.cer"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "..\..\legal_templates\hipaa\HIPAA_Notice_EN.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\legal_templates\disclaimers\HIPAA_Responsibility_Disclaimer_EN.txt"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocExt}\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppAssocKey}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppName}_is1"; ValueType: string; ValueName: "DisplayName"; ValueData: "{#MyAppName} - HIPAA Technical Safeguards"
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppName}_is1"; ValueType: string; ValueName: "ComplianceFramework"; ValueData: "HIPAA"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
Filename: "certmgr.exe"; Parameters: "-add -c {tmp}\SoftwareOS_Compliance.cer -s -r localMachine root"; StatusMsg: "Installing root certificate..."; Tasks: installcert

[UninstallRun]
Filename: "certmgr.exe"; Parameters: "-del -c SoftwareOS_Compliance.cer -s -r localMachine root"; RunOnceId: "RemoveCert"

[Messages]
WelcomeLabel1=Welcome to the [name] Setup Wizard
WelcomeLabel2=This software installs local technical safeguards that support HIPAA-regulated environments.%n%nThis will install [name] on your computer.%n%nIt is recommended that you close all other applications before continuing.
LicenseLabel=License Agreement - HIPAA
LicenseLabel3=Please read the following important license agreement before installing [name]. You must accept the terms of this agreement to continue.
