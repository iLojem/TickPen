using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;

namespace TickPen.Models;

public enum DrawingTool
{
    Cursor,
    Pen,
    Highlighter,
    Line,
    Arrow,
    Rectangle,
    Ellipse,
    Text,
    Eraser
}

public enum EraserMode
{
    ByStroke,
    ByPoint
}

public enum ScreenshotDestination
{
    SavePng,
    CopyToClipboard,
    Both
}

/// <summary>
/// Contrato de comando reversible para unificar el historial Undo/Redo
/// entre trazos de InkCanvas y elementos vectoriales del Canvas.
/// </summary>
public interface ICanvasAction
{
    void Undo();
    void Redo();
}

public sealed class AddStrokeAction : ICanvasAction
{
    private readonly StrokeCollection _strokes;
    private readonly Stroke _stroke;

    public AddStrokeAction(StrokeCollection strokes, Stroke stroke)
    {
        _strokes = strokes;
        _stroke = stroke;
    }

    public void Undo()
    {
        if (_strokes.Contains(_stroke))
            _strokes.Remove(_stroke);
    }

    public void Redo()
    {
        if (!_strokes.Contains(_stroke))
            _strokes.Add(_stroke);
    }
}

public sealed class ModifyStrokesAction : ICanvasAction
{
    private readonly StrokeCollection _target;
    private readonly List<Stroke> _added;
    private readonly List<Stroke> _removed;

    public ModifyStrokesAction(StrokeCollection target, IEnumerable<Stroke> added, IEnumerable<Stroke> removed)
    {
        _target = target;
        _added = added.ToList();
        _removed = removed.ToList();
    }

    public void Undo()
    {
        foreach (var stroke in _added)
        {
            if (_target.Contains(stroke))
                _target.Remove(stroke);
        }

        foreach (var stroke in _removed)
        {
            if (!_target.Contains(stroke))
                _target.Add(stroke);
        }
    }

    public void Redo()
    {
        foreach (var stroke in _removed)
        {
            if (_target.Contains(stroke))
                _target.Remove(stroke);
        }

        foreach (var stroke in _added)
        {
            if (!_target.Contains(stroke))
                _target.Add(stroke);
        }
    }
}

public sealed class AddElementAction : ICanvasAction
{
    private readonly Canvas _canvas;
    private readonly UIElement _element;

    public AddElementAction(Canvas canvas, UIElement element)
    {
        _canvas = canvas;
        _element = element;
    }

    public void Undo()
    {
        if (_canvas.Children.Contains(_element))
            _canvas.Children.Remove(_element);
    }

    public void Redo()
    {
        if (!_canvas.Children.Contains(_element))
            _canvas.Children.Add(_element);
    }
}

public sealed class RemoveElementAction : ICanvasAction
{
    private readonly Canvas _canvas;
    private readonly UIElement _element;

    public RemoveElementAction(Canvas canvas, UIElement element)
    {
        _canvas = canvas;
        _element = element;
    }

    public void Undo()
    {
        if (!_canvas.Children.Contains(_element))
            _canvas.Children.Add(_element);
    }

    public void Redo()
    {
        if (_canvas.Children.Contains(_element))
            _canvas.Children.Remove(_element);
    }
}

public sealed class ClearCanvasAction : ICanvasAction
{
    private readonly StrokeCollection _strokes;
    private readonly Canvas _canvas;
    private readonly List<Stroke> _clearedStrokes;
    private readonly List<UIElement> _clearedElements;

    public ClearCanvasAction(
        StrokeCollection strokes,
        Canvas canvas,
        IEnumerable<Stroke> clearedStrokes,
        IEnumerable<UIElement> clearedElements)
    {
        _strokes = strokes;
        _canvas = canvas;
        _clearedStrokes = clearedStrokes.ToList();
        _clearedElements = clearedElements.ToList();
    }

    public void Undo()
    {
        foreach (var stroke in _clearedStrokes)
        {
            if (!_strokes.Contains(stroke))
                _strokes.Add(stroke);
        }

        foreach (var element in _clearedElements)
        {
            if (!_canvas.Children.Contains(element))
                _canvas.Children.Add(element);
        }
    }

    public void Redo()
    {
        foreach (var stroke in _clearedStrokes)
        {
            if (_strokes.Contains(stroke))
                _strokes.Remove(stroke);
        }

        foreach (var element in _clearedElements)
        {
            if (_canvas.Children.Contains(element))
                _canvas.Children.Remove(element);
        }
    }
}
