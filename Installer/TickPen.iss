; =============================================================================
; Inno Setup Script - TickPen (.NET 8 WPF Self-Contained)
; Requiere Inno Setup 6.x (https://jrsoftware.org/isinfo.php)
; =============================================================================

#define MyAppName "TickPen"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "TickPen Open Source"
#define MyAppExeName "TickPen.exe"

[Setup]
; GUID único para identificar la aplicación en Windows (actualizaciones / desinstalación)
AppId={{8F4C2A91-6E3B-4D17-9A5E-2B8F7C4D91E2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
SetupIconFile=..\Assets\TickPen.ico
; Salida del instalador compilado
OutputDir=..\dist
OutputBaseFilename=TickPen-Setup-v{#MyAppVersion}-win-x64
; Compresión máxima para reducir tamaño del ejecutable autocontenido
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Permite instalar sin privilegios de administrador si el usuario lo prefiere
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Iniciar {#MyAppName} automáticamente con Windows"; GroupDescription: "Opciones adicionales:"; Flags: unchecked

[Files]
; Toma el ejecutable único autocontenido generado por `dotnet publish`
Source: "..\publish\win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Menú Inicio
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
; Acceso directo en el Escritorio (si el usuario marcó la casilla)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
; Inicio automático con Windows (opcional)
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
