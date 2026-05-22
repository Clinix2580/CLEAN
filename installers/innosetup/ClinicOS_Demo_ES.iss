; Inno Setup Script - ClinicOS Demo ES
; MX local demo distribution - Read-Only

#define MyAppName "ClinicOS_Demo"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "SoftwareOS"
#define MyAppExeName "ClinicOS_Demo.exe"

[Setup]
AppId={{CLINICOS-DEMO-ES-2026-001}
AppName={#MyAppName} (Demo - Español)
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppComments=Demo Version - Read-Only Mode. Data will not be saved.
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\..\legal_templates\lfpdppp\EULA_ES.txt
OutputDir=..\..\Distribucion\Installers
OutputBaseFilename=ClinicOS_Demo_ES_Setup
SetupIconFile=..\..\assets\icon.ico
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
Source: "..\..\Distribucion\ClinicOS_Demo_ES\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\Distribucion\ClinicOS_Demo_ES\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\certificates\SoftwareOS_Compliance.cer"; DestDir: "{app}\certificates"; Flags: ignoreversion
Source: "..\..\legal_templates\disclaimers\LFPDPPP_Descargo_ES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Messages]
WelcomeLabel2=VERSION DE DEMOSTRACION - SOLO LECTURA%n%nEste demo muestra salvaguardas locales para MX.%n%nNOTA IMPORTANTE: Esta es una version demo. Todos los datos son de solo lectura y no se guardaran permanentemente.%n%n[name] se instalara en su computadora.
