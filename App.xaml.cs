using System.Windows;
using TickPen.Views;

namespace TickPen;

public partial class App : Application
{
    private OverlayWindow? _overlayWindow;
    private ToolbarWindow? _toolbarWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Crear y mostrar primero el lienzo transparente (inicia en Modo Cursor / Click-Through)
        _overlayWindow = new OverlayWindow();
        _overlayWindow.Show();

        // 2. Crear la barra flotante y vincularla como ventana hija del lienzo
        //    para garantizar que siempre permanezca por encima en el Z-Order.
        _toolbarWindow = new ToolbarWindow(_overlayWindow)
        {
            Owner = _overlayWindow
        };

        // Posicionar inicialmente en el borde derecho de la pantalla
        var workArea = SystemParameters.WorkArea;
        _toolbarWindow.Left = workArea.Right - 92;
        _toolbarWindow.Top = workArea.Top + Math.Max(40, (workArea.Height - 620) / 2);

        // Cerrar toda la aplicación de forma limpia cuando se cierre la barra flotante
        _toolbarWindow.Closed += (_, _) =>
        {
            if (_overlayWindow != null && _overlayWindow.IsLoaded)
            {
                _overlayWindow.Close();
            }
            Shutdown();
        };

        MainWindow = _toolbarWindow;
        _toolbarWindow.Show();
    }
}
