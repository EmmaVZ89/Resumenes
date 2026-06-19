using System.Windows;
using System.Windows.Controls;
using Resumenes.Ui.Servicios;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Vistas;

/// <summary>
/// Pantalla de confirmación de temas detectados.
/// El ViewModel es el DataContext; el parámetro (ParametroTemas) se consume en Loaded.
/// </summary>
public partial class VistaConfirmarTemas : Page
{
    private readonly ConfirmarTemasVm _vm;
    private readonly ServicioNavegacion _nav;

    public VistaConfirmarTemas(ConfirmarTemasVm vm, ServicioNavegacion nav)
    {
        _vm = vm;
        _nav = nav;
        InitializeComponent();
        DataContext = vm;
        Loaded += OnCargado;
    }

    private void OnCargado(object sender, RoutedEventArgs e)
    {
        Loaded -= OnCargado;
        if (_nav.ConsumirParametro() is ParametroTemas param)
            _vm.CargarTemas(param.An, param.TemasDetectados);
    }
}
