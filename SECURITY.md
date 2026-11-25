# Política de Seguridad de TickPen

## Garantías de Seguridad y Privacidad

TickPen está diseñado bajo el principio de **mínimo privilegio y operación 100% offline**:

1. **Cero tráfico de red:** La aplicación no abre sockets, no utiliza `HttpClient`, no consume APIs externas (ni de IA ni de terceros) y no envía telemetría.
2. **Sin credenciales ni secretos:** No solicita ni almacena contraseñas, tokens ni claves API.
3. **Acceso local mínimo:** Solo escribe en disco cuando el usuario elige explícitamente guardar una captura de pantalla mediante el diálogo nativo de Windows (`SaveFileDialog`).
4. **Uso transparente de Win32 API (P/Invoke):**
   - `user32.dll` (`GetWindowLongPtr`, `SetWindowLongPtr`): activa/desactiva `WS_EX_TRANSPARENT` para el modo *Click-Through* del lienzo.
   - `user32.dll` (`RegisterHotKey`, `UnregisterHotKey`): registra los 3 atajos globales (`Ctrl+Shift+D`, `Ctrl+Shift+H`, `Ctrl+Shift+C`) y los libera al cerrar.
   - `gdi32.dll` (`DeleteObject`): libera inmediatamente los handles GDI temporales al copiar capturas al portapapeles.
5. **Dependencias mínimas:** Únicamente utiliza el paquete oficial de Microsoft `System.Drawing.Common` (`8.0.10`) sobre .NET 8 WPF.
6. **Sin privilegios de administrador:** Tanto la aplicación como el instalador pueden ejecutarse y usarse sin elevación (`PrivilegesRequired=lowest`).

## Aviso sobre Windows SmartScreen

Al ser un proyecto open-source independiente sin certificado comercial de firma de código (*Code Signing*), Windows SmartScreen puede mostrar una advertencia preventiva la primera vez que se ejecuta un binario descargado. Cualquier usuario puede auditar el código fuente y compilar su propio `TickPen.exe` directamente con `dotnet publish`.

## Cómo Reportar una Vulnerabilidad

Si encuentras un problema de seguridad, por favor repórtalo de forma privada a través de la pestaña **Security → Advisories** del repositorio en GitHub en lugar de abrir un Issue público.
