using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using TickPen.Interop;
using TickPen.Models;

namespace TickPen.Views;

public partial class ToolbarWindow : Window
{
    private readonly OverlayWindow _overlay;
    private HwndSource? _hwndSource;
    private bool _isCollapsed;
    private EraserMode _currentEraserMode = EraserMode.ByStroke;

    private readonly Dictionary<DrawingTool, Button> _toolButtons;
    private readonly Dictionary<Button, double> _thicknessMap;
    private readonly Dictionary<Button, double> _fadeDelayMap;

    // Geometrías para el icono de Ocultar / Mostrar tinta
    private static readonly Geometry EyeOpenGeometry = Geometry.Parse(
        "M12,4.5 C7,4.5 2.73,7.61 1,12 C2.73,16.39 7,19.5 12,19.5 C17,19.5 21.27,16.39 23,12 C21.27,7.61 17,4.5 12,4.5 Z M12,17 C9.24,17 7,14.76 7,12 C7,9.24 9.24,7 12,7 C14.76,7 17,9.24 17,12 C17,14.76 14.76,17 12,17 Z M12,9 C10.34,9 9,10.34 9,12 C9,13.66 10.34,15 12,15 C13.66,15 15,13.66 15,12 C15,10.34 13.66,9 12,9 Z");

    private static readonly Geometry EyeClosedGeometry = Geometry.Parse(
        "M12,7 C14.76,7 17,9.24 17,12 C17,12.65 16.87,13.26 16.64,13.83 L19.56,16.75 C21.07,15.49 22.26,13.86 23,12 C21.27,7.61 17,4.5 12,4.5 C10.6,4.5 9.26,4.75 8.02,5.2 L10.17,7.35 C10.74,7.13 11.35,7 12,7 Z M2,4.27 L4.28,6.55 L4.74,7.01 C3.08,8.3 1.78,10.02 1,12 C2.73,16.39 7,19.5 12,19.5 C13.55,19.5 15.03,19.2 16.38,18.66 L16.8,19.08 L19.73,22 L21,20.73 L3.27,3 L2,4.27 Z");

    public ToolbarWindow(OverlayWindow overlay)
    {
        InitializeComponent();
        _overlay = overlay;

        _toolButtons = new Dictionary<DrawingTool, Button>
        {
            [DrawingTool.Cursor] = BtnCursor,
            [DrawingTool.Pen] = BtnPen,
            [DrawingTool.Highlighter] = BtnHighlighter,
            [DrawingTool.Eraser] = BtnEraser,
            [DrawingTool.Line] = BtnLine,
            [DrawingTool.Arrow] = BtnArrow,
            [DrawingTool.Rectangle] = BtnRectangle,
            [DrawingTool.Ellipse] = BtnEllipse,
            [DrawingTool.Text] = BtnText
        };

        _thicknessMap = new Dictionary<Button, double>
        {
            [BtnSize1] = 2.0,
            [BtnSize2] = 4.0,
            [BtnSize3] = 8.0,
            [BtnSize4] = 14.0
        };

        _fadeDelayMap = new Dictionary<Button, double>
        {
            [BtnFade2s] = 2.0,
            [BtnFade4s] = 4.0,
            [BtnFade6s] = 6.0,
            [BtnFade10s] = 10.0
        };

        // Suscribirse a eventos del lienzo para mantener la UI siempre sincronizada
        _overlay.HistoryChanged += OnOverlayHistoryChanged;
        _overlay.ToolChanged += OnOverlayToolChanged;
        _overlay.FadeModeChanged += OnOverlayFadeModeChanged;
        _overlay.RequestToolbarVisibility += visible =>
        {
            Dispatcher.Invoke(() => Visibility = visible ? Visibility.Visible : Visibility.Hidden);
        };

        // Estado visual inicial
        SelectInitialDefaults();
    }

    private void SelectInitialDefaults()
    {
        UpdateActiveToolVisual(DrawingTool.Cursor);
        UpdateEraserModeVisual(_currentEraserMode);
        ApplySelectedFadeDelay(BtnFade4s);
        UpdateFadeModeVisual(false);

        // Seleccionar primer color (Rojo) y segundo grosor (4 px) por defecto
        if (ColorGrid.Children.Count > 0 && ColorGrid.Children[0] is Button firstColor)
        {
            ApplySelectedColor(firstColor);
        }
        ApplySelectedThickness(BtnSize2);
    }

    #region Global Hotkeys (Win32 HwndSource)

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);

        NativeMethods.RegisterDefaultHotkeys(hwnd);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_hwndSource != null)
        {
            NativeMethods.UnregisterDefaultHotkeys(_hwndSource.Handle);
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        base.OnClosed(e);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int hotkeyId = wParam.ToInt32();
            switch (hotkeyId)
            {
                case NativeMethods.HOTKEY_TOGGLE_DRAW:
                    _overlay.ToggleCursorAndDrawMode();
                    handled = true;
                    break;

                case NativeMethods.HOTKEY_TOGGLE_TOOLBAR:
                    // Usar Hidden en lugar de Close mantiene vivo el HWND para volver a mostrarla
                    Visibility = Visibility == Visibility.Visible
                        ? Visibility.Hidden
                        : Visibility.Visible;
                    handled = true;
                    break;

                case NativeMethods.HOTKEY_CLEAR_SCREEN:
                    _overlay.ClearAll();
                    handled = true;
                    break;

                case NativeMethods.HOTKEY_TOGGLE_FADE:
                    _overlay.ToggleFadeMode();
                    handled = true;
                    break;
            }
        }

        return IntPtr.Zero;
    }

    #endregion

    #region Header (Drag, Collapse, Close)

    private void DragHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void BtnCollapse_Click(object sender, RoutedEventArgs e)
    {
        _isCollapsed = !_isCollapsed;
        ToolbarBody.Visibility = _isCollapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapseIcon.Text = _isCollapsed ? "▼" : "▲";
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Tool Selection Handlers

    private void BtnCursor_Click(object sender, RoutedEventArgs e) => _overlay.SetTool(DrawingTool.Cursor);
    private void BtnPen_Click(object sender, RoutedEventArgs e) => _overlay.SetTool(DrawingTool.Pen);
    private void BtnHighlighter_Click(object sender, RoutedEventArgs e) => _overlay.SetTool(DrawingTool.Highlighter);
    private void BtnEraser_Click(object sender, RoutedEventArgs e) => _overlay.SetTool(DrawingTool.Eraser);
    private void BtnLine_Click(object sender, RoutedEventArgs e) => _overlay.SetTool(DrawingTool.Line);
    private void BtnArrow_Click(object sender, RoutedEventArgs e) => _overlay.SetTool(DrawingTool.Arrow);
    private void BtnRectangle_Click(object sender, RoutedEventArgs e) => _overlay.SetTool(DrawingTool.Rectangle);
    private void BtnEllipse_Click(object sender, RoutedEventArgs e) => _overlay.SetTool(DrawingTool.Ellipse);
    private void BtnText_Click(object sender, RoutedEventArgs e) => _overlay.SetTool(DrawingTool.Text);

    private void BtnEraseStroke_Click(object sender, RoutedEventArgs e)
    {
        _currentEraserMode = EraserMode.ByStroke;
        _overlay.SetEraserMode(_currentEraserMode);
        UpdateEraserModeVisual(_currentEraserMode);
    }

    private void BtnErasePoint_Click(object sender, RoutedEventArgs e)
    {
        _currentEraserMode = EraserMode.ByPoint;
        _overlay.SetEraserMode(_currentEraserMode);
        UpdateEraserModeVisual(_currentEraserMode);
    }

    private void OnOverlayToolChanged(DrawingTool tool)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateActiveToolVisual(tool);
            EraserModePanel.Visibility = tool == DrawingTool.Eraser
                ? Visibility.Visible
                : Visibility.Collapsed;
            UpdateInkVisibilityIcon();
        });
    }

    private void UpdateActiveToolVisual(DrawingTool activeTool)
    {
        foreach (var (tool, button) in _toolButtons)
        {
            button.Tag = tool == activeTool ? "Active" : null;
        }
    }

    private void UpdateEraserModeVisual(EraserMode mode)
    {
        BtnEraseStroke.Tag = mode == EraserMode.ByStroke ? "Active" : null;
        BtnErasePoint.Tag = mode == EraserMode.ByPoint ? "Active" : null;
    }

    private void BtnFadeMode_Click(object sender, RoutedEventArgs e)
    {
        _overlay.ToggleFadeMode();
    }

    private void FadeDelay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            ApplySelectedFadeDelay(btn);
        }
    }

    private void OnOverlayFadeModeChanged(bool enabled)
    {
        Dispatcher.Invoke(() => UpdateFadeModeVisual(enabled));
    }

    private void UpdateFadeModeVisual(bool enabled)
    {
        BtnFadeMode.Tag = enabled ? "Active" : null;
        FadeBtnLabel.Text = enabled ? $"Fade {(int)_overlay.FadeDelaySeconds}s" : "Fade";
        FadeDurationPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplySelectedFadeDelay(Button selectedButton)
    {
        foreach (var (btn, seconds) in _fadeDelayMap)
        {
            bool isSelected = ReferenceEquals(btn, selectedButton);
            btn.Tag = isSelected ? "Active" : null;
            if (isSelected)
            {
                _overlay.SetFadeDelay(seconds);
            }
        }

        if (_overlay.IsFadeEnabled)
        {
            FadeBtnLabel.Text = $"Fade {(int)_overlay.FadeDelaySeconds}s";
        }
    }

    #endregion

    #region History, Visibility & Clear

    private void BtnUndo_Click(object sender, RoutedEventArgs e) => _overlay.Undo();
    private void BtnRedo_Click(object sender, RoutedEventArgs e) => _overlay.Redo();
    private void BtnClear_Click(object sender, RoutedEventArgs e) => _overlay.ClearAll();

    private void BtnToggleInk_Click(object sender, RoutedEventArgs e)
    {
        _overlay.ToggleInkVisibility();
        UpdateInkVisibilityIcon();
    }

    private void UpdateInkVisibilityIcon()
    {
        bool visible = _overlay.IsInkVisible;
        InkVisibilityIcon.Data = visible ? EyeOpenGeometry : EyeClosedGeometry;
        BtnToggleInk.Tag = visible ? null : "Active";
    }

    private void OnOverlayHistoryChanged(bool canUndo, bool canRedo)
    {
        Dispatcher.Invoke(() =>
        {
            BtnUndo.IsEnabled = canUndo;
            BtnRedo.IsEnabled = canRedo;
        });
    }

    #endregion

    #region Color & Thickness Selectors

    private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            ApplySelectedColor(btn);
        }
    }

    private void ApplySelectedColor(Button selectedButton)
    {
        foreach (UIElement child in ColorGrid.Children)
        {
            if (child is Button btn)
            {
                btn.Tag = ReferenceEquals(btn, selectedButton) ? "Active" : null;
            }
        }

        if (selectedButton.Background is SolidColorBrush brush)
        {
            _overlay.SetColor(brush.Color);
        }
    }

    private void Thickness_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            ApplySelectedThickness(btn);
        }
    }

    private void ApplySelectedThickness(Button selectedButton)
    {
        foreach (var (btn, thickness) in _thicknessMap)
        {
            bool isSelected = ReferenceEquals(btn, selectedButton);
            btn.Tag = isSelected ? "Active" : null;
            if (isSelected)
            {
                _overlay.SetThickness(thickness);
            }
        }
    }

    #endregion

    #region Screenshot Menu

    private void BtnScreenshot_Click(object sender, RoutedEventArgs e)
    {
        if (Resources["ScreenshotMenu"] is ContextMenu menu)
        {
            menu.PlacementTarget = BtnScreenshot;
            menu.Placement = PlacementMode.Left;
            menu.IsOpen = true;
        }
    }

    private void MenuCaptureRegionClipboard_Click(object sender, RoutedEventArgs e)
        => _overlay.StartRegionCapture(ScreenshotDestination.CopyToClipboard);

    private void MenuCaptureRegionSave_Click(object sender, RoutedEventArgs e)
        => _overlay.StartRegionCapture(ScreenshotDestination.SavePng);

    private async void MenuCaptureFullClipboard_Click(object sender, RoutedEventArgs e)
        => await _overlay.CaptureFullScreenAsync(ScreenshotDestination.CopyToClipboard);

    private async void MenuCaptureFullSave_Click(object sender, RoutedEventArgs e)
        => await _overlay.CaptureFullScreenAsync(ScreenshotDestination.SavePng);

    #endregion

    #region Local Keyboard Shortcuts

    private void ToolbarWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.Z)
            {
                _overlay.Undo();
                e.Handled = true;
            }
            else if (e.Key == Key.Y)
            {
                _overlay.Redo();
                e.Handled = true;
            }
        }
    }

    #endregion
}
