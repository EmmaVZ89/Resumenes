using System.Windows;
using System.Windows.Controls;
using Resumenes.Ui.Servicios;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Vistas;

public partial class VistaExamenes : Page
{
    private readonly ExamenesVm _vm;
    private readonly ServicioNavegacion _nav;

    public VistaExamenes(ExamenesVm vm, ServicioNavegacion nav)
    {
        _vm = vm; _nav = nav;
        InitializeComponent();
        DataContext = vm;
        Loaded += OnCargado;
    }

    private void OnCargado(object sender, RoutedEventArgs e)
    {
        Loaded -= OnCargado;
        if (_nav.ConsumirParametro() is ParametroExamenes p) _vm.Cargar(p.An);
    }
}
