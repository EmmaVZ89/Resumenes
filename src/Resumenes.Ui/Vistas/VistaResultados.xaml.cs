using System.Windows;
using System.Windows.Controls;
using Resumenes.Ui.Servicios;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Vistas;

/// <summary>
/// Pantalla de resultados: muestra los PDFs generados.
/// El ViewModel es el DataContext; el parámetro (ParametroResultados) se consume en Loaded.
/// </summary>
public partial class VistaResultados : Page
{
    private readonly ResultadosVm _vm;
    private readonly ServicioNavegacion _nav;

    public VistaResultados(ResultadosVm vm, ServicioNavegacion nav)
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
        if (_nav.ConsumirParametro() is ParametroResultados param)
            _vm.Cargar(param.An);
    }
}
