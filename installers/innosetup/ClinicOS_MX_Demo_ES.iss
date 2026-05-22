; ClinicOS MX Demo ES - read-only local demo

#define MyAppName "ClinicOS MX Demo"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "SoftwareOS"
#define MyAppExeName "ClinicOS_MX_Demo.exe"

[Setup]
AppId={{CLINICOS-MX-DEMO-ES-2026-001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppComments=Demo MX local - Read-Only Mode.
DefaultDirName={autopf}\ClinicOS\MX\Demo
DefaultGroupName=ClinicOS MX
AllowNoIcons=yes
LicenseFile=..\..\legal_templates\lfpdppp\EULA_ES.txt
OutputDir=..\..\Distribucion\Installers\MX
OutputBaseFilename=ClinicOS_MX_Demo_ES_Setup
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
Source: "..\..\Distribucion\ClinicOS_MX_Demo_ES\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\Distribucion\ClinicOS_MX_Demo_ES\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\legal_templates\disclaimers\LFPDPPP_Descargo_ES.txt"; DestDir: "{app}\legal"; Flags: ignoreversion
Source: "..\..\legal_templates\lfpdppp\Aviso_Privacidad.txt"; DestDir: "{app}\legal"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Messages]
WelcomeLabel2=VERSION DE DEMOSTRACION MX - SOLO LECTURA%n%nEste demo usa datos ficticios y salvaguardas locales para MX.%n%n[name] se instalara en su computadora.
LicenseLabel=Acuerdo de Licencia - MX Demo
