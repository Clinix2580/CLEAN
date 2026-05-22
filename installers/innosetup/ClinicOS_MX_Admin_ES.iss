; ClinicOS MX Admin ES - local license administration

#define MyAppName "ClinicOS MX Admin"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "SoftwareOS"
#define MyAppExeName "ClinicOS_MX_Admin.exe"

[Setup]
AppId={{CLINICOS-MX-ADMIN-ES-2026-001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppComments=Admin MX local - HWID license administration.
DefaultDirName={autopf}\ClinicOS\MX\Admin
DefaultGroupName=ClinicOS MX
AllowNoIcons=yes
LicenseFile=..\..\legal_templates\lfpdppp\EULA_ES.txt
OutputDir=..\..\Distribucion\Installers\MX
OutputBaseFilename=ClinicOS_MX_Admin_ES_Setup
SetupIconFile=..\..\ClinicOS.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
DisableDirPage=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear icono en escritorio"; GroupDescription: "Iconos adicionales:"; Flags: unchecked

[Files]
Source: "..\..\Distribucion\ClinicOS_MX_Admin_ES\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\Distribucion\ClinicOS_MX_Admin_ES\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\certificates\SoftwareOS_Compliance.cer"; DestDir: "{app}\certificates"; Flags: ignoreversion
Source: "..\..\legal_templates\disclaimers\LFPDPPP_Descargo_ES.txt"; DestDir: "{app}\legal"; Flags: ignoreversion
Source: "..\..\legal_templates\lfpdppp\Aviso_Privacidad.txt"; DestDir: "{app}\legal"; Flags: ignoreversion

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\ClinicOSMXAdmin_is1"; ValueType: string; ValueName: "ComplianceFramework"; ValueData: "LFPDPPP/NOM-024"
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\ClinicOSMXAdmin_is1"; ValueType: string; ValueName: "ClinicOSEdition"; ValueData: "Admin"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Messages]
WelcomeLabel2=ADMINISTRADOR MX%n%nEste modulo administra licencias locales por HWID segun el plan contratado.%n%n[name] se instalara en su computadora.
LicenseLabel=Acuerdo de Licencia - MX Admin
