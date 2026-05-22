; ClinicOS MX Full ES - local clinical runtime

#define MyAppName "ClinicOS MX Full"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "SoftwareOS"
#define MyAppExeName "ClinicOS_MX_Full.exe"

[Setup]
AppId={{CLINICOS-MX-FULL-ES-2026-001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppComments=Full MX local clinical runtime.
DefaultDirName={autopf}\ClinicOS\MX\Full
DefaultGroupName=ClinicOS MX
AllowNoIcons=yes
LicenseFile=..\..\legal_templates\lfpdppp\EULA_ES.txt
OutputDir=..\..\Distribucion\Installers\MX
OutputBaseFilename=ClinicOS_MX_Full_ES_Setup
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
Source: "..\..\Distribucion\ClinicOS_MX_Full_ES\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\Distribucion\ClinicOS_MX_Full_ES\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\certificates\SoftwareOS_Compliance.cer"; DestDir: "{app}\certificates"; Flags: ignoreversion
Source: "..\..\legal_templates\disclaimers\LFPDPPP_Descargo_ES.txt"; DestDir: "{app}\legal"; Flags: ignoreversion
Source: "..\..\legal_templates\lfpdppp\Aviso_Privacidad.txt"; DestDir: "{app}\legal"; Flags: ignoreversion

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\ClinicOSMXFull_is1"; ValueType: string; ValueName: "ComplianceFramework"; ValueData: "LFPDPPP/NOM-024"
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\ClinicOSMXFull_is1"; ValueType: string; ValueName: "ClinicOSEdition"; ValueData: "Full"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Messages]
WelcomeLabel2=CLINICOS MX FULL%n%nEste software instala salvaguardas locales para apoyar LFPDPPP y NOM-024.%n%n[name] se instalara en su computadora.
LicenseLabel=Acuerdo de Licencia - MX Full
