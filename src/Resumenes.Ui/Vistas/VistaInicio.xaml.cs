using System.Windows.Controls;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Vistas;

/// <summary>
/// Pantalla de inicio: muestra el historial de análisis y permite crear uno nuevo.
/// </summary>
public partial class VistaInicio : Page
{
    private readonly InicioVm? _vm;

    public VistaInicio()
    {
        InitializeComponent();
        ActualizarEncabezado();
    }

    /// <summary>Inyecta el ViewModel y lo asigna como DataContext.</summary>
    public VistaInicio(InicioVm vm) : this()
    {
        _vm = vm;
        DataContext = vm;
        // Cargar el historial inmediatamente
        vm.Cargar();
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private void ActualizarEncabezado()
    {
        var hora = DateTime.Now.Hour;
        var saludo = hora < 12 ? "Buenos días" : hora < 20 ? "Buenas tardes" : "Buenas noches";
        TxtSaludo.Text = saludo;
        TxtFecha.Text = DateTime.Now.ToString("dddd, d 'de' MMMM 'de' yyyy",
            new System.Globalization.CultureInfo("es-AR"));
    }
}
