; =============================================================================
; Inno Setup Script - TickPen (.NET 8 WPF Self-Contained)
; Asistente clásico de instalación de Windows (Next -> Next -> Elegir ruta)
; =============================================================================

#define MyAppName "TickPen"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "TickPen Open Source"
#define MyAppURL "https://github.com/iLojem/TickPen"
#define MyAppExeName "TickPen.exe"

[Setup]
; GUID único para identificar la aplicación en Windows (actualizaciones / desinstalación)
AppId={{8F4C2A91-6E3B-4D17-9A5E-2B8F7C4D91E2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}

; Mostrar todas las pantallas clásicas del Wizard (Bienvenida, Licencia, Carpeta, Menú Inicio)
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=no
DisableReadyPage=no
LicenseFile=..\LICENSE
AllowNoIcons=yes

; Identidad visual del instalador (icono y banners con el logo de TickPen)
SetupIconFile=..\Assets\TickPen.ico
WizardStyle=modern
WizardImageFile=wizard-sidebar.bmp
WizardSmallImageFile=wizard-small.bmp
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

; Salida y compresión máxima
OutputDir=..\dist
OutputBaseFilename=TickPen-Setup-v{#MyAppVersion}-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Permite elegir instalación para el usuario actual (sin admin) o para todos los usuarios
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Cierra TickPen automáticamente si está abierto al actualizar/instalar
CloseApplications=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startupicon"; Description: "Iniciar {#MyAppName} automáticamente con Windows"; GroupDescription: "Opciones adicionales:"; Flags: unchecked

[Files]
; Ejecutable único autocontenido generado por `dotnet publish`
Source: "..\publish\win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Menú Inicio
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
; Acceso directo en el Escritorio (marcado por defecto)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
; Inicio automático con Windows (opcional)
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
