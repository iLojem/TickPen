using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TickPen.Interop;
using TickPen.Models;
using Microsoft.Win32;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Path = System.Windows.Shapes.Path;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace TickPen.Views;

public partial class OverlayWindow : Window
{
    // Fondo casi imperceptible para que WPF capture el hit-test del mouse al dibujar
    private static readonly SolidColorBrush CaptureHitTestBrush = new(Color.FromArgb(1, 0, 0, 0));

    // Estado actual de herramienta y estilo
    private DrawingTool _currentTool = DrawingTool.Cursor;
    private DrawingTool _lastDrawingTool = DrawingTool.Pen;
    private EraserMode _eraserMode = EraserMode.ByStroke;
    private Color _currentColor = Color.FromRgb(239, 68, 68); // Rojo vibrante por defecto
    private double _currentThickness = 4.0;

    // Pilas unificadas de Undo / Redo
    private readonly Stack<ICanvasAction> _undoStack = new();
    private readonly Stack<ICanvasAction> _redoStack = new();
    private bool _isInternalStrokeChange;

    // Estado de dibujo vectorial (formas con vista previa)
    private bool _isDrawingShape;
    private Point _shapeStartPoint;
    private UIElement? _previewElement;

    // Estado de borrador sobre elementos vectoriales
    private bool _isErasingVectors;

    // Estado de herramienta de texto activa
    private TextBox? _activeTextBox;

    // Estado de captura de pantalla por región
    private bool _isSelectingRegion;
    private Point _regionStartPoint;
    private ScreenshotDestination _pendingScreenshotDestination;

    /// <summary>Notifica cambios en el historial para habilitar/deshabilitar Undo/Redo en la toolbar.</summary>
    public event Action<bool, bool>? HistoryChanged;

    /// <summary>Notifica a la barra de herramientas cuando cambia la herramienta activa.</summary>
    public event Action<DrawingTool>? ToolChanged;

    /// <summary>Permite ocultar temporalmente la ToolbarWindow durante la captura de pantalla.</summary>
    public event Action<bool>? RequestToolbarVisibility;

    public DrawingTool CurrentTool => _currentTool;
    public bool IsInkVisible => DrawingLayer.Visibility == Visibility.Visible;
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public OverlayWindow()
    {
        InitializeComponent();
        CaptureHitTestBrush.Freeze();

        // Escucha cambios de trazos (necesario para registrar borrados por trazo/punto en el Undo)
        MainInkCanvas.Strokes.StrokesChanged += Strokes_StrokesChanged;

        // Permite borrar también elementos vectoriales cuando el usuario arrastra el borrador
        MainInkCanvas.PreviewMouseLeftButtonDown += MainInkCanvas_EraserMouseDown;
        MainInkCanvas.PreviewMouseMove += MainInkCanvas_EraserMouseMove;
        MainInkCanvas.PreviewMouseLeftButtonUp += MainInkCanvas_EraserMouseUp;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Inicializar flags Win32 base y arrancar en Modo Cursor (Click-Through activo)
        NativeMethods.InitializeOverlayStyles(this);
        SetTool(DrawingTool.Cursor);
    }

    #region Public API (Consumida por ToolbarWindow)

    public void SetTool(DrawingTool tool)
    {
        // Si había un TextBox abierto sin confirmar, fijarlo antes de cambiar de herramienta
        CommitActiveTextBox();
        CancelRegionCapture();

        _currentTool = tool;
        if (tool != DrawingTool.Cursor)
        {
            _lastDrawingTool = tool;
        }

        // Si el usuario selecciona una herramienta de dibujo mientras la tinta estaba oculta, mostrarla
        if (tool != DrawingTool.Cursor && !IsInkVisible)
        {
            SetInkVisibility(true);
        }

        bool isClickThrough = tool == DrawingTool.Cursor;
        NativeMethods.SetClickThrough(this, isClickThrough);
        RootGrid.Background = isClickThrough ? Brushes.Transparent : CaptureHitTestBrush;

        // Configurar qué capa recibe eventos según la herramienta
        switch (tool)
        {
            case DrawingTool.Cursor:
                MainInkCanvas.EditingMode = InkCanvasEditingMode.None;
                MainInkCanvas.IsHitTestVisible = false;
                VectorCanvas.IsHitTestVisible = false;
                Cursor = Cursors.Arrow;
                break;

            case DrawingTool.Pen:
                ConfigureInkAttributes(isHighlighter: false);
                MainInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                MainInkCanvas.IsHitTestVisible = true;
                VectorCanvas.IsHitTestVisible = false;
                Cursor = Cursors.Pen;
                break;

            case DrawingTool.Highlighter:
                ConfigureInkAttributes(isHighlighter: true);
                MainInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                MainInkCanvas.IsHitTestVisible = true;
                VectorCanvas.IsHitTestVisible = false;
                Cursor = Cursors.Pen;
                break;

            case DrawingTool.Eraser:
                ApplyEraserMode();
                MainInkCanvas.IsHitTestVisible = true;
                VectorCanvas.IsHitTestVisible = false;
                Cursor = Cursors.Cross;
                break;

            case DrawingTool.Line:
            case DrawingTool.Arrow:
            case DrawingTool.Rectangle:
            case DrawingTool.Ellipse:
                MainInkCanvas.EditingMode = InkCanvasEditingMode.None;
                MainInkCanvas.IsHitTestVisible = false;
                VectorCanvas.IsHitTestVisible = true;
                Cursor = Cursors.Cross;
                break;

            case DrawingTool.Text:
                MainInkCanvas.EditingMode = InkCanvasEditingMode.None;
                MainInkCanvas.IsHitTestVisible = false;
                VectorCanvas.IsHitTestVisible = true;
                Cursor = Cursors.IBeam;
                break;
        }

        ToolChanged?.Invoke(_currentTool);
    }

    /// <summary>Alterna entre Modo Cursor y la última herramienta de dibujo (Ctrl+Shift+D).</summary>
    public void ToggleCursorAndDrawMode()
    {
        if (_currentTool == DrawingTool.Cursor)
        {
            SetTool(_lastDrawingTool == DrawingTool.Cursor ? DrawingTool.Pen : _lastDrawingTool);
        }
        else
        {
            SetTool(DrawingTool.Cursor);
        }
    }

    public void SetColor(Color color)
    {
        _currentColor = color;

        if (_currentTool == DrawingTool.Pen)
            ConfigureInkAttributes(isHighlighter: false);
        else if (_currentTool == DrawingTool.Highlighter)
            ConfigureInkAttributes(isHighlighter: true);

        if (_activeTextBox != null)
            _activeTextBox.Foreground = new SolidColorBrush(_currentColor);
    }

    public void SetThickness(double thickness)
    {
        _currentThickness = thickness;

        if (_currentTool == DrawingTool.Pen)
            ConfigureInkAttributes(isHighlighter: false);
        else if (_currentTool == DrawingTool.Highlighter)
            ConfigureInkAttributes(isHighlighter: true);
        else if (_currentTool == DrawingTool.Eraser)
            ApplyEraserMode();

        if (_activeTextBox != null)
            _activeTextBox.FontSize = GetFontSizeForThickness(_currentThickness);
    }

    public void SetEraserMode(EraserMode mode)
    {
        _eraserMode = mode;
        if (_currentTool == DrawingTool.Eraser)
        {
            ApplyEraserMode();
        }
    }

    public void Undo()
    {
        CommitActiveTextBox();
        if (_undoStack.Count == 0) return;

        var action = _undoStack.Pop();
        ExecuteInternalStrokeChange(() => action.Undo());
        _redoStack.Push(action);
        NotifyHistoryChanged();
    }

    public void Redo()
    {
        CommitActiveTextBox();
        if (_redoStack.Count == 0) return;

        var action = _redoStack.Pop();
        ExecuteInternalStrokeChange(() => action.Redo());
        _undoStack.Push(action);
        NotifyHistoryChanged();
    }

    public void ClearAll()
    {
        CommitActiveTextBox();

        if (MainInkCanvas.Strokes.Count == 0 && VectorCanvas.Children.Count == 0)
            return;

        var strokesSnapshot = MainInkCanvas.Strokes.ToList();
        var elementsSnapshot = VectorCanvas.Children.Cast<UIElement>().ToList();

        ExecuteInternalStrokeChange(() =>
        {
            MainInkCanvas.Strokes.Clear();
            VectorCanvas.Children.Clear();
        });

        PushAction(new ClearCanvasAction(MainInkCanvas.Strokes, VectorCanvas, strokesSnapshot, elementsSnapshot));
    }

    public void ToggleInkVisibility()
    {
        SetInkVisibility(!IsInkVisible);
    }

    public void SetInkVisibility(bool visible)
    {
        DrawingLayer.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
    }

    #endregion

    #region InkCanvas (Pen, Highlighter & Eraser)

    private void ConfigureInkAttributes(bool isHighlighter)
    {
        var attrs = new DrawingAttributes
        {
            Color = _currentColor,
            FitToCurve = true,
            IgnorePressure = false,
            IsHighlighter = isHighlighter,
            StylusTip = isHighlighter ? StylusTip.Rectangle : StylusTip.Ellipse,
            Width = isHighlighter ? Math.Max(14.0, _currentThickness * 3.0) : _currentThickness,
            Height = isHighlighter ? Math.Max(22.0, _currentThickness * 4.5) : _currentThickness
        };

        MainInkCanvas.DefaultDrawingAttributes = attrs;
    }

    private void ApplyEraserMode()
    {
        double eraserSize = Math.Max(16.0, _currentThickness * 3.5);
        MainInkCanvas.EraserShape = new EllipseStylusShape(eraserSize, eraserSize);
        MainInkCanvas.EditingMode = _eraserMode == EraserMode.ByStroke
            ? InkCanvasEditingMode.EraseByStroke
            : InkCanvasEditingMode.EraseByPoint;
    }

    private void MainInkCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        // El registro en Undo se centraliza en Strokes_StrokesChanged para cubrir también EraseByPoint
    }

    private void Strokes_StrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
    {
        if (_isInternalStrokeChange) return;

        if (e.Added.Count == 1 && e.Removed.Count == 0)
        {
            PushAction(new AddStrokeAction(MainInkCanvas.Strokes, e.Added[0]));
        }
        else if (e.Added.Count > 0 || e.Removed.Count > 0)
        {
            PushAction(new ModifyStrokesAction(MainInkCanvas.Strokes, e.Added, e.Removed));
        }
    }

    // Soporte de borrador también para formas geométricas y texto en VectorCanvas
    private void MainInkCanvas_EraserMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentTool != DrawingTool.Eraser) return;
        _isErasingVectors = true;
        TryEraseVectorAt(e.GetPosition(VectorCanvas));
    }

    private void MainInkCanvas_EraserMouseMove(object sender, MouseEventArgs e)
    {
        if (_currentTool != DrawingTool.Eraser || !_isErasingVectors || e.LeftButton != MouseButtonState.Pressed)
            return;

        TryEraseVectorAt(e.GetPosition(VectorCanvas));
    }

    private void MainInkCanvas_EraserMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isErasingVectors = false;
    }

    private void TryEraseVectorAt(Point position)
    {
        double hitRadius = Math.Max(10.0, _currentThickness * 2.0);
        var hitGeometry = new EllipseGeometry(position, hitRadius, hitRadius);
        UIElement? hitElement = null;

        bool prevHitTest = VectorCanvas.IsHitTestVisible;
        VectorCanvas.IsHitTestVisible = true;
        try
        {
            VisualTreeHelper.HitTest(
                VectorCanvas,
                null,
                result =>
                {
                    if (result.VisualHit is DependencyObject visual)
                    {
                        var topChild = FindDirectChildOfCanvas(VectorCanvas, visual);
                        if (topChild != null)
                        {
                            hitElement = topChild;
                            return HitTestResultBehavior.Stop;
                        }
                    }
                    return HitTestResultBehavior.Continue;
                },
                new GeometryHitTestParameters(hitGeometry));
        }
        finally
        {
            VectorCanvas.IsHitTestVisible = prevHitTest;
        }

        if (hitElement != null)
        {
            VectorCanvas.Children.Remove(hitElement);
            PushAction(new RemoveElementAction(VectorCanvas, hitElement));
        }
    }

    private static UIElement? FindDirectChildOfCanvas(Canvas canvas, DependencyObject current)
    {
        while (current != null)
        {
            var parent = VisualTreeHelper.GetParent(current);
            if (ReferenceEquals(parent, canvas))
                return current as UIElement;

            current = parent!;
        }
        return null;
    }

    #endregion

    #region Vector Shapes & Text Tool

    private void VectorCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Point pos = e.GetPosition(VectorCanvas);

        if (_currentTool == DrawingTool.Text)
        {
            // Si había un TextBox abierto y se hace clic fuera de él, primero lo fijamos
            if (_activeTextBox != null)
            {
                CommitActiveTextBox();
                return;
            }

            SpawnTextInputAt(pos);
            return;
        }

        if (_currentTool is DrawingTool.Line or DrawingTool.Arrow or DrawingTool.Rectangle or DrawingTool.Ellipse)
        {
            _isDrawingShape = true;
            _shapeStartPoint = pos;
            _previewElement = CreateShapeElement(_currentTool, pos, pos);

            if (_previewElement != null)
            {
                VectorCanvas.Children.Add(_previewElement);
                VectorCanvas.CaptureMouse();
            }
        }
    }

    private void VectorCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDrawingShape || _previewElement == null) return;

        Point currentPoint = e.GetPosition(VectorCanvas);

        // Mantener Shift restringe proporciones (cuadrados, círculos o líneas a 45°)
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            currentPoint = ApplyShiftConstraint(_shapeStartPoint, currentPoint, _currentTool);
        }

        UpdateShapeElement(_previewElement, _currentTool, _shapeStartPoint, currentPoint);
    }

    private void VectorCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawingShape) return;

        VectorCanvas.ReleaseMouseCapture();
        _isDrawingShape = false;

        Point endPoint = e.GetPosition(VectorCanvas);
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            endPoint = ApplyShiftConstraint(_shapeStartPoint, endPoint, _currentTool);
        }

        // Descartar formas diminutas producto de un clic accidental
        double distance = (_shapeStartPoint - endPoint).Length;
        if (_previewElement != null)
        {
            if (distance < 3.0)
            {
                VectorCanvas.Children.Remove(_previewElement);
            }
            else
            {
                UpdateShapeElement(_previewElement, _currentTool, _shapeStartPoint, endPoint);
                PushAction(new AddElementAction(VectorCanvas, _previewElement));
            }
        }

        _previewElement = null;
    }

    private UIElement? CreateShapeElement(DrawingTool tool, Point start, Point end)
    {
        var brush = new SolidColorBrush(_currentColor);
        brush.Freeze();

        return tool switch
        {
            DrawingTool.Line => new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = brush,
                StrokeThickness = _currentThickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            },
            DrawingTool.Arrow => new Path
            {
                Stroke = brush,
                Fill = brush,
                StrokeThickness = _currentThickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Data = BuildArrowGeometry(start, end, _currentThickness)
            },
            DrawingTool.Rectangle => CreateBoundedShape<Rectangle>(brush, start),
            DrawingTool.Ellipse => CreateBoundedShape<Ellipse>(brush, start),
            _ => null
        };
    }

    private T CreateBoundedShape<T>(SolidColorBrush strokeBrush, Point origin) where T : Shape, new()
    {
        var shape = new T
        {
            Stroke = strokeBrush,
            StrokeThickness = _currentThickness,
            Fill = Brushes.Transparent,
            Width = 0,
            Height = 0
        };
        Canvas.SetLeft(shape, origin.X);
        Canvas.SetTop(shape, origin.Y);
        return shape;
    }

    private void UpdateShapeElement(UIElement element, DrawingTool tool, Point start, Point end)
    {
        switch (tool)
        {
            case DrawingTool.Line when element is Line line:
                line.X2 = end.X;
                line.Y2 = end.Y;
                break;

            case DrawingTool.Arrow when element is Path arrowPath:
                arrowPath.Data = BuildArrowGeometry(start, end, _currentThickness);
                break;

            case DrawingTool.Rectangle or DrawingTool.Ellipse when element is Shape shape:
                double left = Math.Min(start.X, end.X);
                double top = Math.Min(start.Y, end.Y);
                double width = Math.Abs(end.X - start.X);
                double height = Math.Abs(end.Y - start.Y);
                Canvas.SetLeft(shape, left);
                Canvas.SetTop(shape, top);
                shape.Width = width;
                shape.Height = height;
                break;
        }
    }

    /// <summary>
    /// Construye la geometría vectorial de una flecha (eje + punta triangular calculada por ángulo).
    /// </summary>
    private static Geometry BuildArrowGeometry(Point start, Point end, double thickness)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();

        // Línea principal
        ctx.BeginFigure(start, isFilled: false, isClosed: false);
        ctx.LineTo(end, isStroked: true, isSmoothJoin: false);

        Vector delta = end - start;
        if (delta.Length >= 4.0)
        {
            double angle = Math.Atan2(delta.Y, delta.X);
            double headLength = Math.Max(14.0, thickness * 3.8);
            double headAngle = Math.PI / 6.5; // ~27.7 grados

            Point wing1 = new(
                end.X - headLength * Math.Cos(angle - headAngle),
                end.Y - headLength * Math.Sin(angle - headAngle));

            Point wing2 = new(
                end.X - headLength * Math.Cos(angle + headAngle),
                end.Y - headLength * Math.Sin(angle + headAngle));

            ctx.BeginFigure(end, isFilled: true, isClosed: true);
            ctx.LineTo(wing1, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(wing2, isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Point ApplyShiftConstraint(Point start, Point end, DrawingTool tool)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;

        if (tool is DrawingTool.Rectangle or DrawingTool.Ellipse)
        {
            double size = Math.Max(Math.Abs(dx), Math.Abs(dy));
            return new Point(start.X + Math.Sign(dx) * size, start.Y + Math.Sign(dy) * size);
        }

        // Para línea y flecha, ajustar al múltiplo de 45° más cercano
        double angle = Math.Atan2(dy, dx);
        double snappedAngle = Math.Round(angle / (Math.PI / 4.0)) * (Math.PI / 4.0);
        double length = Math.Sqrt(dx * dx + dy * dy);
        return new Point(
            start.X + length * Math.Cos(snappedAngle),
            start.Y + length * Math.Sin(snappedAngle));
    }

    private void SpawnTextInputAt(Point position)
    {
        var textBox = new TextBox
        {
            Background = new SolidColorBrush(Color.FromArgb(40, 15, 23, 42)),
            Foreground = new SolidColorBrush(_currentColor),
            CaretBrush = new SolidColorBrush(_currentColor),
            BorderBrush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 3, 6, 3),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            FontSize = GetFontSizeForThickness(_currentThickness),
            MinWidth = 120,
            AcceptsReturn = false
        };

        Canvas.SetLeft(textBox, position.X);
        Canvas.SetTop(textBox, position.Y);

        textBox.KeyDown += (s, args) =>
        {
            if (args.Key == Key.Enter)
            {
                CommitActiveTextBox();
                args.Handled = true;
            }
            else if (args.Key == Key.Escape)
            {
                CancelActiveTextBox();
                args.Handled = true;
            }
        };

        textBox.LostFocus += (s, args) => CommitActiveTextBox();

        _activeTextBox = textBox;
        VectorCanvas.Children.Add(textBox);

        // Activar la ventana temporalmente para recibir foco de teclado
        Activate();
        textBox.Focus();
    }

    private void CommitActiveTextBox()
    {
        if (_activeTextBox == null) return;

        var textBox = _activeTextBox;
        _activeTextBox = null;

        string content = textBox.Text.Trim();
        double left = Canvas.GetLeft(textBox);
        double top = Canvas.GetTop(textBox);

        VectorCanvas.Children.Remove(textBox);

        if (string.IsNullOrEmpty(content)) return;

        var textBlock = new TextBlock
        {
            Text = content,
            Foreground = textBox.Foreground.Clone(),
            FontFamily = textBox.FontFamily,
            FontWeight = textBox.FontWeight,
            FontSize = textBox.FontSize,
            Padding = new Thickness(6, 3, 6, 3),
            IsHitTestVisible = true
        };

        Canvas.SetLeft(textBlock, left);
        Canvas.SetTop(textBlock, top);

        VectorCanvas.Children.Add(textBlock);
        PushAction(new AddElementAction(VectorCanvas, textBlock));
    }

    private void CancelActiveTextBox()
    {
        if (_activeTextBox == null) return;

        VectorCanvas.Children.Remove(_activeTextBox);
        _activeTextBox = null;
    }

    private static double GetFontSizeForThickness(double thickness) => thickness switch
    {
        <= 3 => 18,
        <= 6 => 24,
        <= 10 => 32,
        _ => 42
    };

    #endregion

    #region Screenshot (Full Screen & Region)

    /// <summary>
    /// Captura la pantalla completa incluyendo las aplicaciones inferiores y las anotaciones visibles.
    /// </summary>
    public async Task CaptureFullScreenAsync(ScreenshotDestination destination)
    {
        CommitActiveTextBox();
        var bounds = GetPhysicalScreenBounds();
        await CapturePhysicalRegionAsync(bounds, destination);
    }

    /// <summary>
    /// Activa el modo de selección de región rectangular sobre el lienzo.
    /// </summary>
    public void StartRegionCapture(ScreenshotDestination destination)
    {
        CommitActiveTextBox();
        _pendingScreenshotDestination = destination;
        _isSelectingRegion = false;

        // Desactivar click-through temporalmente para poder arrastrar la selección
        NativeMethods.SetClickThrough(this, false);
        RootGrid.Background = CaptureHitTestBrush;

        SelectionRect.Visibility = Visibility.Collapsed;
        SelectionSizeBadge.Visibility = Visibility.Collapsed;
        CaptureOverlayCanvas.Visibility = Visibility.Visible;
        Activate();
    }

    private void CancelRegionCapture()
    {
        if (CaptureOverlayCanvas.Visibility != Visibility.Visible) return;

        _isSelectingRegion = false;
        CaptureOverlayCanvas.ReleaseMouseCapture();
        CaptureOverlayCanvas.Visibility = Visibility.Collapsed;
        SelectionRect.Visibility = Visibility.Collapsed;
        SelectionSizeBadge.Visibility = Visibility.Collapsed;
    }

    private void CaptureOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isSelectingRegion = true;
        _regionStartPoint = e.GetPosition(CaptureOverlayCanvas);

        Canvas.SetLeft(SelectionRect, _regionStartPoint.X);
        Canvas.SetTop(SelectionRect, _regionStartPoint.Y);
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        SelectionRect.Visibility = Visibility.Visible;
        SelectionSizeBadge.Visibility = Visibility.Visible;

        CaptureOverlayCanvas.CaptureMouse();
    }

    private void CaptureOverlay_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSelectingRegion) return;

        Point current = e.GetPosition(CaptureOverlayCanvas);
        double left = Math.Min(_regionStartPoint.X, current.X);
        double top = Math.Min(_regionStartPoint.Y, current.Y);
        double width = Math.Abs(current.X - _regionStartPoint.X);
        double height = Math.Abs(current.Y - _regionStartPoint.Y);

        Canvas.SetLeft(SelectionRect, left);
        Canvas.SetTop(SelectionRect, top);
        SelectionRect.Width = width;
        SelectionRect.Height = height;

        SelectionSizeText.Text = $"{(int)width} × {(int)height}";
        Canvas.SetLeft(SelectionSizeBadge, left);
        Canvas.SetTop(SelectionSizeBadge, Math.Max(4, top - 28));
    }

    private async void CaptureOverlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelectingRegion) return;

        _isSelectingRegion = false;
        CaptureOverlayCanvas.ReleaseMouseCapture();

        Point end = e.GetPosition(CaptureOverlayCanvas);
        double left = Math.Min(_regionStartPoint.X, end.X);
        double top = Math.Min(_regionStartPoint.Y, end.Y);
        double width = Math.Abs(end.X - _regionStartPoint.X);
        double height = Math.Abs(end.Y - _regionStartPoint.Y);

        CaptureOverlayCanvas.Visibility = Visibility.Collapsed;
        SelectionRect.Visibility = Visibility.Collapsed;
        SelectionSizeBadge.Visibility = Visibility.Collapsed;

        // Restaurar el modo de herramienta previo
        SetTool(_currentTool);

        if (width < 5 || height < 5) return;

        // Convertir coordenadas lógicas WPF a píxeles físicos reales según el DPI del monitor
        Point topLeftPhysical = PointToScreen(new Point(left, top));
        Point bottomRightPhysical = PointToScreen(new Point(left + width, top + height));

        var physicalRect = new System.Drawing.Rectangle(
            (int)Math.Round(topLeftPhysical.X),
            (int)Math.Round(topLeftPhysical.Y),
            Math.Max(1, (int)Math.Round(bottomRightPhysical.X - topLeftPhysical.X)),
            Math.Max(1, (int)Math.Round(bottomRightPhysical.Y - topLeftPhysical.Y)));

        await CapturePhysicalRegionAsync(physicalRect, _pendingScreenshotDestination);
    }

    private async Task CapturePhysicalRegionAsync(System.Drawing.Rectangle physicalRect, ScreenshotDestination destination)
    {
        // Ocultar temporalmente la barra flotante para que no aparezca en la captura
        RequestToolbarVisibility?.Invoke(false);

        // Esperar un breve frame para que DWM componga la pantalla sin la barra ni el selector
        await Task.Delay(120);

        try
        {
            using var bitmap = new Bitmap(physicalRect.Width, physicalRect.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(
                    physicalRect.Left,
                    physicalRect.Top,
                    0,
                    0,
                    physicalRect.Size,
                    CopyPixelOperation.SourceCopy);
            }

            if (destination is ScreenshotDestination.CopyToClipboard or ScreenshotDestination.Both)
            {
                CopyBitmapToClipboard(bitmap);
            }

            if (destination is ScreenshotDestination.SavePng or ScreenshotDestination.Both)
            {
                SaveBitmapWithDialog(bitmap);
            }
        }
        finally
        {
            RequestToolbarVisibility?.Invoke(true);
        }
    }

    private System.Drawing.Rectangle GetPhysicalScreenBounds()
    {
        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        int width = (int)Math.Round(SystemParameters.PrimaryScreenWidth * dpiX);
        int height = (int)Math.Round(SystemParameters.PrimaryScreenHeight * dpiY);
        return new System.Drawing.Rectangle(0, 0, width, height);
    }

    private static void CopyBitmapToClipboard(Bitmap bitmap)
    {
        IntPtr hBitmap = bitmap.GetHbitmap();
        try
        {
            BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bitmapSource.Freeze();
            Clipboard.SetImage(bitmapSource);
        }
        finally
        {
            NativeMethods.DeleteObject(hBitmap);
        }
    }

    private static void SaveBitmapWithDialog(Bitmap bitmap)
    {
        string defaultFolder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "TickPenCaptures");
        Directory.CreateDirectory(defaultFolder);

        var dialog = new SaveFileDialog
        {
            Title = "Guardar captura de pantalla",
            InitialDirectory = defaultFolder,
            FileName = $"Captura_{DateTime.Now:yyyyMMdd_HHmmss}.png",
            Filter = "Imagen PNG (*.png)|*.png",
            DefaultExt = ".png"
        };

        if (dialog.ShowDialog() == true)
        {
            bitmap.Save(dialog.FileName, ImageFormat.Png);
        }
    }

    #endregion

    #region Keyboard Shortcuts & Undo/Redo Helpers

    private void OverlayWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Si estamos editando texto, dejar que el TextBox maneje Ctrl+Z / Escape
        if (_activeTextBox != null) return;

        if (e.Key == Key.Escape && CaptureOverlayCanvas.Visibility == Visibility.Visible)
        {
            CancelRegionCapture();
            SetTool(_currentTool);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.Z)
            {
                Undo();
                e.Handled = true;
            }
            else if (e.Key == Key.Y)
            {
                Redo();
                e.Handled = true;
            }
        }
    }

    private void PushAction(ICanvasAction action)
    {
        _undoStack.Push(action);
        _redoStack.Clear();
        NotifyHistoryChanged();
    }

    private void ExecuteInternalStrokeChange(Action action)
    {
        _isInternalStrokeChange = true;
        try
        {
            action();
        }
        finally
        {
            _isInternalStrokeChange = false;
        }
    }

    private void NotifyHistoryChanged()
    {
        HistoryChanged?.Invoke(CanUndo, CanRedo);
    }

    #endregion
}
