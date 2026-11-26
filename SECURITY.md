# Security Policy

## Security & Privacy Guarantees

TickPen is designed around **least privilege and 100% offline operation**:

1. **Zero network traffic:** The application opens no sockets, does not use `HttpClient`, consumes no external or AI APIs, and collects zero telemetry.
2. **No credentials or secrets:** Never requests, stores, or transmits passwords, tokens, or API keys.
3. **Minimal disk access:** Only writes to disk when the user explicitly saves a screenshot through the native Windows `SaveFileDialog`.
4. **Transparent Win32 API (P/Invoke) usage:**
   - `user32.dll` (`GetWindowLongPtr`, `SetWindowLongPtr`): toggles `WS_EX_TRANSPARENT` for native *Click-Through* mode on the overlay window.
   - `user32.dll` (`RegisterHotKey`, `UnregisterHotKey`): registers the global shortcuts (`Ctrl+Shift+D`, `Ctrl+Shift+F`, `Ctrl+Shift+H`, `Ctrl+Shift+C`) and unregisters them cleanly on exit.
   - `gdi32.dll` (`DeleteObject`): immediately frees temporary GDI bitmap handles after copying screenshots to the clipboard.
5. **Minimal dependencies:** Uses only Microsoft's official `System.Drawing.Common` (`8.0.10`) package on top of .NET 8 WPF.
6. **No admin rights required:** Both the application and the Inno Setup wizard run without administrator elevation by default (`PrivilegesRequired=lowest`).

## Windows SmartScreen Notice

Because TickPen is an independent open-source project without a commercial code-signing certificate, Windows SmartScreen may show a preventive warning the first time a downloaded binary is executed. Anyone can audit the full source code and build their own `TickPen.exe` locally using `dotnet publish`.

## Reporting a Vulnerability

If you discover a security issue, please report it privately through GitHub's **Security → Advisories** tab rather than opening a public issue.
