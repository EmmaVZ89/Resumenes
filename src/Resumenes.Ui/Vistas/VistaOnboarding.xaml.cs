using System.Windows.Controls;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Vistas;

/// <summary>
/// Pantalla de bienvenida / primer arranque.
/// Verifica el estado del runtime y guía al usuario para completar la configuración mínima.
/// </summary>
public partial class VistaOnboarding : Page
{
    public VistaOnboarding()
    {
        InitializeComponent();
    }

    /// <summary>Inyecta el ViewModel y lo asigna como DataContext.</summary>
    public VistaOnboarding(OnboardingVm vm) : this()
    {
        DataContext = vm;
    }
}
