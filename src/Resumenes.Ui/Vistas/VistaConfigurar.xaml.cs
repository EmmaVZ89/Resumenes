using System.Windows.Controls;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Vistas;

/// <summary>
/// Pantalla de configuración de un análisis nuevo.
/// Permite seleccionar carpeta, ver archivos y configurar el prompt de temas.
/// </summary>
public partial class VistaConfigurar : Page
{
    public VistaConfigurar()
    {
        InitializeComponent();
    }

    /// <summary>Inyecta el ViewModel y lo asigna como DataContext.</summary>
    public VistaConfigurar(ConfigurarVm vm) : this()
    {
        DataContext = vm;
    }
}
