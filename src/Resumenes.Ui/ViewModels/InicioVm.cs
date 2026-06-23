using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;
using Resumenes.Infrastructure.Aplicacion;
using Resumenes.Ui.Servicios;
using Resumenes.Ui.Vistas;

namespace Resumenes.Ui.ViewModels;

/// <summary>
/// ViewModel de la pantalla de Inicio: muestra el historial de análisis
/// y permite crear uno nuevo, retomar o eliminar uno existente.
/// </summary>
public partial class InicioVm : VistaModeloBase
{
    private readonly IRepositorioEstado _repo;
    private readonly ServicioNavegacion _nav;
    private readonly IServicioAnalisis _servicio;
    private readonly Configuracion _cfg;
    private readonly Wpf.Ui.IContentDialogService _dialogos;
    private readonly ServicioCostos _costos;
    private readonly ServicioActualizaciones? _actualizaciones;

    [ObservableProperty]
    private ObservableCollection<AnalisisHistorialVm> _analisis = new();

    /// <summary>True si hay una versión más nueva publicada (muestra el aviso en Inicio).</summary>
    [ObservableProperty]
    private bool _hayActualizacion;

    /// <summary>Texto del aviso de actualización.</summary>
    [ObservableProperty]
    private string _mensajeActualizacion = string.Empty;

    private string _urlActualizacion = string.Empty;

    public InicioVm(IRepositorioEstado repo, ServicioNavegacion nav, IServicioAnalisis servicio,
        Configuracion cfg, Wpf.Ui.IContentDialogService dialogos, ServicioCostos costos,
        ServicioActualizaciones? actualizaciones = null)
    {
        _repo = repo;
        _nav = nav;
        _servicio = servicio;
        _cfg = cfg;
        _dialogos = dialogos;
        _costos = costos;
        _actualizaciones = actualizaciones;
    }

    /// <summary>
    /// Crea un ContentDialog con el estilo de la app y un borde externo de acento que
    /// contrasta sobre el velo, tanto en modo oscuro como claro.
    /// </summary>
    private static Wpf.Ui.Controls.ContentDialog CrearDialogo(string titulo, string contenido)
    {
        var d = new Wpf.Ui.Controls.ContentDialog
        {
            Title = titulo,
            Content = contenido,
            BorderThickness = new System.Windows.Thickness(1),
        };
        if (System.Windows.Application.Current?.TryFindResource("SystemAccentColorBrush") is System.Windows.Media.Brush b)
            d.BorderBrush = b;
        return d;
    }

    private Task<Wpf.Ui.Controls.ContentDialogResult> AvisarAsync(string titulo, string mensaje)
    {
        var d = CrearDialogo(titulo, mensaje);
        d.CloseButtonText = "Aceptar";
        return _dialogos.ShowAsync(d, CancellationToken.None);
    }

    /// <summary>
    /// Carga el historial desde el repositorio y puebla la colección.
    /// Llamar al navegar a esta vista.
    /// </summary>
    public void Cargar()
    {
        var lista = _repo.ListarAnalisis();
        Analisis.Clear();
        foreach (var an in lista)
            Analisis.Add(new AnalisisHistorialVm(an, _costos.CostoLegible(an.Id)));
    }

    /// <summary>
    /// Chequea (best-effort, una sola vez por sesión) si hay una versión más nueva en GitHub.
    /// Si la hay, prende el aviso. Silencioso si no hay internet o falla.
    /// </summary>
    public async Task ChequearActualizacionAsync()
    {
        if (_actualizaciones is null) return;
        var info = await _actualizaciones.ChequearAsync();
        if (info is null) return;
        _urlActualizacion = info.Url;
        MensajeActualizacion = $"Versión {info.Version} disponible. Hacé clic en Descargar para obtenerla.";
        HayActualizacion = true;
    }

    /// <summary>Abre la página de la release en el navegador para descargar el instalador.</summary>
    [RelayCommand]
    private void DescargarActualizacion()
    {
        if (string.IsNullOrWhiteSpace(_urlActualizacion)) return;
        try { Process.Start(new ProcessStartInfo { FileName = _urlActualizacion, UseShellExecute = true }); }
        catch { /* si no se puede abrir el navegador, ignorar */ }
    }

    /// <summary>Descarta el aviso de actualización por esta sesión.</summary>
    [RelayCommand]
    private void DescartarActualizacion() => HayActualizacion = false;

    // ── Comandos globales ────────────────────────────────────────────────

    /// <summary>Navega a la pantalla de configuración para iniciar un análisis nuevo.</summary>
    [RelayCommand]
    private void NuevoAnalisis()
    {
        _nav.Navegar<VistaConfigurar>();
    }

    // ── Comandos por ítem ────────────────────────────────────────────────

    /// <summary>Abre la carpeta de salida del análisis en el Explorador de Windows.</summary>
    [RelayCommand]
    private void AbrirCarpeta(AnalisisHistorialVm? item)
    {
        if (item is null) return;
        // Convención: <workspace>/analisis/<id>/final  (workspace ya es ruta absoluta).
        var carpeta = System.IO.Path.Combine(_cfg.RutaWorkspace, "analisis", item.Id, "final");
        if (!System.IO.Directory.Exists(carpeta))
            carpeta = item.CarpetaOrigen;
        if (!System.IO.Directory.Exists(carpeta)) return; // nada que abrir

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = carpeta,
                UseShellExecute = true
            });
        }
        catch { /* Si la carpeta no se puede abrir, ignorar */ }
    }

    /// <summary>Elimina el análisis (en cascada) tras confirmar con un ContentDialog temático.</summary>
    [RelayCommand]
    private async Task Eliminar(AnalisisHistorialVm? item)
    {
        if (item is null) return;

        var dialogo = CrearDialogo("Eliminar análisis",
            $"¿Eliminar el análisis «{item.Nombre}»?\n\n" +
            "Se borrará del historial junto con sus archivos intermedios y temas. " +
            "Los PDFs ya exportados a otra carpeta no se tocan.");
        dialogo.PrimaryButtonText = "Eliminar";
        dialogo.PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger;
        dialogo.CloseButtonText = "Cancelar";

        if (await _dialogos.ShowAsync(dialogo, CancellationToken.None) != Wpf.Ui.Controls.ContentDialogResult.Primary)
            return;

        _repo.EliminarAnalisis(item.Id);
        // Quitar de la colección observable (refresca el ItemsControl y el estado vacío).
        Analisis.Remove(item);
    }

    /// <summary>
    /// Continúa un análisis en proceso: recarga la lista de archivos de origen en el servicio
    /// (necesario porque el servicio es stateful) y reanuda el pipeline idempotente en VistaEjecutando.
    /// </summary>
    [RelayCommand]
    private async Task Continuar(AnalisisHistorialVm? item)
    {
        if (item is null) return;
        var an = _repo.ListarAnalisis().FirstOrDefault(a => a.Id == item.Id);
        if (an is null) return;

        if (!System.IO.Directory.Exists(an.CarpetaOrigen))
        {
            await AvisarAsync("Carpeta no encontrada",
                $"La carpeta de origen ya no existe:\n{an.CarpetaOrigen}\n\nNo se puede continuar este análisis.");
            return;
        }

        try
        {
            // AbrirOCrearAsync vuelve a escanear la carpeta y carga el estado del servicio;
            // sin esto, ProcesarArchivosAsync no tendría archivos que procesar.
            var actual = await _servicio.AbrirOCrearAsync(an.CarpetaOrigen, CancellationToken.None);
            _nav.Navegar<VistaEjecutando>(new ParametroEjecucion(actual, ""));
        }
        catch (Exception ex)
        {
            await AvisarAsync("Error", $"No se pudo reanudar el análisis:\n{ex.Message}");
        }
    }

    /// <summary>
    /// Muestra los resultados de un análisis completado navegando a VistaResultados.
    /// </summary>
    [RelayCommand]
    private void VerResultados(AnalisisHistorialVm? item)
    {
        if (item is null) return;
        var an = _repo.ListarAnalisis().FirstOrDefault(a => a.Id == item.Id);
        if (an is null) return;
        _nav.Navegar<VistaResultados>(new ParametroResultados(an));
    }
}
