<p align="center">
  <img src="Assets/TickPenLogo.png" alt="TickPen Logo" width="120" />
</p>

<h1 align="center">TickPen</h1>

<p align="center">
  <strong>Lightweight, native, 100% offline screen annotation tool for Windows.</strong><br/>
  Built for <strong>traders, financial analysts, educators, and live presenters</strong>.
</p>

<p align="center">
  <a href="https://github.com/iLojem/TickPen/releases/latest">
    <img src="https://img.shields.io/badge/Download%20for%20Windows-v1.0.0-2563EB?style=for-the-badge&logo=windows" alt="Download TickPen for Windows" />
  </a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0%20WPF-512BD4?logo=dotnet" alt=".NET 8 WPF" />
  <img src="https://img.shields.io/badge/Platform-Windows%20x64-0078D6?logo=windows" alt="Windows x64" />
  <img src="https://img.shields.io/badge/Offline-100%25%20Local-16A34A" alt="100% Offline" />
  <img src="https://img.shields.io/badge/License-MIT-blue" alt="MIT License" />
</p>

---

## Download

- **[Download Windows Installer (`TickPen-Setup-v1.0.0-win-x64.exe`)](https://github.com/iLojem/TickPen/releases/latest/download/TickPen-Setup-v1.0.0-win-x64.exe)** *(Recommended — guided setup wizard with Desktop/Start Menu shortcuts and uninstaller)*
- **[Download Portable (`TickPen.exe`)](https://github.com/iLojem/TickPen/releases/latest/download/TickPen.exe)** *(Single self-contained executable — no installation or .NET runtime required)*
- Browse all releases: [GitHub Releases](https://github.com/iLojem/TickPen/releases)

> **Windows SmartScreen Notice:** Because TickPen is an independent open-source project without a paid code-signing certificate, Windows may show a preventive warning on first run. Click **More info → Run anyway**, or build from source below.

---

## Key Features

- **Native Win32 Click-Through Mode:** Interact seamlessly with TradingView, MetaTrader, Excel, PowerPoint, or any active window while keeping your annotations visible on screen.
- **Auto-Fade Mode (ScreenBrush-style):** Draw temporary callouts and highlights that smoothly fade out after **`2s`, `4s`, `6s`, or `10s`** — ideal for live trading sessions and presentations without manual erasing.
- **Smooth Pen & Highlighter:** Hardware-accelerated WPF `InkCanvas` with curve smoothing (`FitToCurve`) and translucent highlighter strokes.
- **Vector Shapes with Live Preview:** Draw **Lines**, **Arrows** (auto-calculated arrowheads), **Rectangles**, and **Ellipses** *(hold `Shift` for squares, circles, or 45° angles)*.
- **Inline Text Tool:** Click anywhere on screen to type directly over charts or slides (`Enter` to commit, `Esc` to cancel).
- **Dual Eraser:** Switch between **Erase by Stroke** and **Erase by Point** (works on both ink strokes and vector shapes/text).
- **Unified Undo / Redo:** `Ctrl+Z` and `Ctrl+Y` across ink strokes, shapes, text, and even *Clear Screen*.
- **Hide / Show Ink:** Temporarily hide your annotations without losing your markup.
- **Built-in Screenshot Capture:** Capture a selected region or the full screen (including your annotations) directly to the clipboard or save as PNG.
- **Compact Collapsible Toolbar:** Dark floating UI that stays on top, drags smoothly, and never gets in the way.

---

## Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Ctrl + Shift + D` | Toggle between **Cursor Mode** (*Click-Through*) and **Draw Mode** (global) |
| `Ctrl + Shift + F` | Toggle **Auto-Fade Mode** (global) |
| `Ctrl + Shift + H` | Hide / Show the floating toolbar (global) |
| `Ctrl + Shift + C` | Clear entire screen (global) |
| `Ctrl + Z` / `Ctrl + Y` | Undo / Redo |
| `Shift` *(while drawing shapes)* | Constrain to squares, circles, or 45° lines |

---

## Privacy & Security

TickPen is **100% local and offline**:
- **Zero network traffic:** Makes no HTTP requests, uses no external APIs, and sends no telemetry.
- **Auditable Win32 usage:** Only calls `user32.dll` (Click-Through and global hotkeys) and `gdi32.dll` (safe GDI handle cleanup when copying screenshots to clipboard).

See [SECURITY.md](SECURITY.md) for the full security policy.

---

## Build from Source

### Prerequisites
- Windows 10 / 11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or newer)

### Run in development
```powershell
dotnet run
```

### Publish single-file self-contained `TickPen.exe`
```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o .\publish\win-x64
```

### Build the Windows Setup Wizard (Optional)
With [Inno Setup 6](https://jrsoftware.org/isinfo.php) installed:
```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" .\Installer\TickPen.iss
```

---

## License

Distributed under the **MIT License**. See [LICENSE](LICENSE) for more information.
