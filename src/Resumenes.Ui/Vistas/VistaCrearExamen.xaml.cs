using System.Windows;
using System.Windows.Controls;
using Resumenes.Ui.Servicios;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Vistas;

public partial class VistaCrearExamen : Page
{
    private readonly CrearExamenVm _vm;
    private readonly ServicioNavegacion _nav;

    public VistaCrearExamen(CrearExamenVm vm, ServicioNavegacion nav)
    {
        _vm = vm;
        _nav = nav;
        InitializeComponent();
        DataContext = vm;

        // Sincronizar RadioButton "Completo" con FuenteRapida invertido
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CrearExamenVm.FuenteRapida))
                RbCompleto.IsChecked = !_vm.FuenteRapida;
        };
        RbCompleto.Checked += (_, _) => _vm.FuenteRapida = false;

        Loaded += OnCargado;
    }

    private void OnCargado(object sender, RoutedEventArgs e)
    {
        Loaded -= OnCargado;
        if (_nav.ConsumirParametro() is ParametroCrearExamen p) _vm.Cargar(p.An);
        // Inicializar estado del RadioButton "Completo"
        RbCompleto.IsChecked = !_vm.FuenteRapida;
    }
}
