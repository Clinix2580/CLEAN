; Inno Setup Script - ClinicOS Full ES
; MX local clinical distribution - LFPDPPP/NOM-024 safeguards

#define MyAppName "ClinicOS"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "SoftwareOS"
#define MyAppExeName "ClinicOS.exe"
#define MyAppAssocName MyAppName + " Database"
#define MyAppAssocExt ".clinicdb"
#define MyAppAssocKey StringChange(MyAppAssocName, " ", "") + MyAppAssocExt

[Setup]
AppId={{CLINICOS-FULL-ES-2026-001}
AppName={#MyAppName} (Full - Español)
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\..\legal_templates\lfpdppp\EULA_ES.txt
OutputDir=..\..\Distribucion\Installers
OutputBaseFilename=ClinicOS_Full_ES_Setup
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
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "installcert"; Description: "Instalar certificado de confianza"; GroupDescription: "Seguridad:"; Flags: checked
Name: "firewallexception"; Description: "{cm:CreateFirewallException}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\..\Distribucion\ClinicOS_Full_ES\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\Distribucion\ClinicOS_Full_ES\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\certificates\SoftwareOS_Compliance.cer"; DestDir: "{app}\certificates"; Flags: ignoreversion
Source: "..\..\certificates\SoftwareOS_Compliance.cer"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "..\..\legal_templates\lfpdppp\Aviso_Privacidad.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\legal_templates\disclaimers\LFPDPPP_Descargo_ES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocExt}\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppAssocKey}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppName}_is1"; ValueType: string; ValueName: "DisplayName"; ValueData: "{#MyAppName} - MX Local Safeguards"
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppName}_is1"; ValueType: string; ValueName: "ComplianceFramework"; ValueData: "LFPDPPP/NOM-024"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
Filename: "certmgr.exe"; Parameters: "-add -c {tmp}\SoftwareOS_Compliance.cer -s -r localMachine root"; StatusMsg: "Instalando certificado raíz..."; Tasks: installcert

[UninstallRun]
Filename: "certmgr.exe"; Parameters: "-del -c SoftwareOS_Compliance.cer -s -r localMachine root"; RunOnceId: "RemoveCert"

[Messages]
WelcomeLabel1=Bienvenido al asistente de instalación de [name]
WelcomeLabel2=Este software instala salvaguardas locales para apoyar LFPDPPP y NOM-024.%n%nSe instalará [name] en su computadora.%n%nSe recomienda cerrar todas las demás aplicaciones antes de continuar.
LicenseLabel=Acuerdo de Licencia - MX
LicenseLabel3=Lea el siguiente acuerdo de licencia importante antes de instalar [name]. Debe aceptar los términos de este acuerdo para continuar.
