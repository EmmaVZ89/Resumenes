using Resumenes.Core.Interfaces;
using Resumenes.Ui.Servicios;
using Resumenes.Ui.Vistas;
using Wpf.Ui.Controls;

namespace Resumenes.Ui;

public partial class MainWindow : FluentWindow
{
    private readonly ServicioNavegacion _servicioNavegacion;
    private readonly IAlmacenSecretos _secretos;

    public MainWindow(ServicioNavegacion servicioNavegacion, IAlmacenSecretos secretos,
        Wpf.Ui.IContentDialogService dialogService)
    {
        _servicioNavegacion = servicioNavegacion;
        _secretos = secretos;
        InitializeComponent();
        _servicioNavegacion.AsociarNavigationView(Nav);
        // Registrar el host de los ContentDialog (diálogos modales con velo).
        dialogService.SetDialogHost(PresentadorDialogos);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // Si falta la API key (requisito esencial), mostrar Onboarding.
        // Si ya está configurada, ir directamente a Inicio.
        if (_secretos.ObtenerApiKey() is null)
            _servicioNavegacion.Navegar<VistaOnboarding>();
        else
            _servicioNavegacion.Navegar<VistaInicio>();
    }
}
