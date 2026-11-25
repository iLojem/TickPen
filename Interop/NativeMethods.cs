using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TickPen.Interop;

/// <summary>
/// Envoltorio seguro de P/Invoke sobre user32.dll para gestionar:
/// 1) Modo Click-Through (WS_EX_TRANSPARENT + WS_EX_LAYERED).
/// 2) Ocultación en Alt+Tab (WS_EX_TOOLWINDOW).
/// 3) Atajos globales del sistema (RegisterHotKey / UnregisterHotKey).
/// </summary>
public static class NativeMethods
{
    // Índices de estilos de ventana
    public const int GWL_EXSTYLE = -20;

    // Extended Window Styles
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    // Mensajes Win32
    public const int WM_HOTKEY = 0x0312;

    // Modificadores para RegisterHotKey
    public const uint MOD_NONE = 0x0000;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // Códigos Virtual-Key (VK_*)
    public const uint VK_C = 0x43;
    public const uint VK_D = 0x44;
    public const uint VK_F = 0x46;
    public const uint VK_H = 0x48;

    // Identificadores internos de atajos globales
    public const int HOTKEY_TOGGLE_DRAW = 9001;    // Ctrl + Shift + D
    public const int HOTKEY_TOGGLE_TOOLBAR = 9002; // Ctrl + Shift + H
    public const int HOTKEY_CLEAR_SCREEN = 9003;   // Ctrl + Shift + C
    public const int HOTKEY_TOGGLE_FADE = 9004;    // Ctrl + Shift + F

    #region P/Invoke Declarations

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr hObject);

    #endregion

    #region 32/64-bit Safe Wrappers

    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    #endregion

    #region High-Level WPF Helpers

    /// <summary>
    /// Configura los estilos extendidos base del lienzo (WS_EX_LAYERED | WS_EX_TOOLWINDOW)
    /// para que funcione la transparencia por píxel y no aparezca en Alt+Tab.
    /// </summary>
    public static void InitializeOverlayStyles(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        exStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));
    }

    /// <summary>
    /// Activa o desactiva el modo Click-Through sobre una ventana WPF.
    /// Cuando enable == true, todos los eventos de mouse atraviesan la ventana hacia Windows.
    /// </summary>
    public static void SetClickThrough(Window window, bool enable)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        exStyle |= WS_EX_LAYERED;

        if (enable)
        {
            exStyle |= WS_EX_TRANSPARENT;
        }
        else
        {
            exStyle &= ~WS_EX_TRANSPARENT;
        }

        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));
    }

    /// <summary>
    /// Registra los 3 atajos globales estándar (Ctrl+Shift+D, Ctrl+Shift+H, Ctrl+Shift+C)
    /// sobre el handle de la ventana especificada (normalmente ToolbarWindow).
    /// </summary>
    public static void RegisterDefaultHotkeys(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        uint modifiers = MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT;
        RegisterHotKey(hwnd, HOTKEY_TOGGLE_DRAW, modifiers, VK_D);
        RegisterHotKey(hwnd, HOTKEY_TOGGLE_TOOLBAR, modifiers, VK_H);
        RegisterHotKey(hwnd, HOTKEY_CLEAR_SCREEN, modifiers, VK_C);
        RegisterHotKey(hwnd, HOTKEY_TOGGLE_FADE, modifiers, VK_F);
    }

    /// <summary>
    /// Libera los atajos globales registrados al cerrar la aplicación.
    /// </summary>
    public static void UnregisterDefaultHotkeys(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        UnregisterHotKey(hwnd, HOTKEY_TOGGLE_DRAW);
        UnregisterHotKey(hwnd, HOTKEY_TOGGLE_TOOLBAR);
        UnregisterHotKey(hwnd, HOTKEY_CLEAR_SCREEN);
        UnregisterHotKey(hwnd, HOTKEY_TOGGLE_FADE);
    }

    #endregion
}
