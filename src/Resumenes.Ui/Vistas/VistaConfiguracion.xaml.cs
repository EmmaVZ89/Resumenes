using System.Windows.Controls;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Vistas;

/// <summary>
/// Pantalla de configuración de la aplicación.
/// Permite editar API key, carpeta de salida, DPI, modelo y tema.
/// </summary>
public partial class VistaConfiguracion : Page
{
    public VistaConfiguracion()
    {
        InitializeComponent();
    }

    /// <summary>Inyecta el ViewModel y lo asigna como DataContext.</summary>
    public VistaConfiguracion(ConfiguracionVm vm) : this()
    {
        DataContext = vm;
    }
}
