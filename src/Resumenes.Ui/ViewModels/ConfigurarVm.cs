using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Resumenes.Core.Interfaces;
using Resumenes.Ui.Servicios;
using Resumenes.Ui.Vistas;

namespace Resumenes.Ui.ViewModels;

/// <summary>
/// ViewModel de la pantalla de configuración de un análisis nuevo.
/// Permite seleccionar carpeta, ver archivos detectados y configurar el prompt de temas.
/// </summary>
public partial class ConfigurarVm : VistaModeloBase
{
    private static readonly string[] ExtensionesAceptadas =
        { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".txt" };

    private readonly IServicioAnalisis _servicio;
    private readonly ServicioNavegacion _nav;

    [ObservableProperty]
    private string _carpetaSeleccionada = string.Empty;

    [ObservableProperty]
    private string _promptTemas = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _archivos = new();

    [ObservableProperty]
    private bool _analizando;

    [ObservableProperty]
    private string _mensajeError = string.Empty;

    public ConfigurarVm(IServicioAnalisis servicio, ServicioNavegacion nav)
    {
        _servicio = servicio;
        _nav = nav;
    }

    // ── Comandos ────────────────────────────────────────────────────────

    /// <summary>Abre un diálogo para seleccionar la carpeta de entrada.</summary>
    [RelayCommand]
    private void SeleccionarCarpeta()
    {
        var dialogo = new OpenFolderDialog
        {
            Title = "Seleccionar carpeta con archivos para analizar",
            Multiselect = false
        };

        if (dialogo.ShowDialog() == true)
        {
            CarpetaSeleccionada = dialogo.FolderName;
            CargarArchivos(dialogo.FolderName);
        }
    }

    /// <summary>
    /// Inicia el análisis: abre/crea el Analisis en el repositorio y navega a VistaEjecutando.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeAnalizar))]
    private async Task Analizar()
    {
        if (string.IsNullOrWhiteSpace(CarpetaSeleccionada)) return;

        Analizando = true;
        MensajeError = string.Empty;
        try
        {
            var an = await _servicio.AbrirOCrearAsync(CarpetaSeleccionada, CancellationToken.None);
            var param = new ParametroEjecucion(an, PromptTemas);
            _nav.Navegar<VistaEjecutando>(param);
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al preparar el análisis: {ex.Message}";
        }
        finally
        {
            Analizando = false;
        }
    }

    private bool PuedeAnalizar() =>
        !string.IsNullOrWhiteSpace(CarpetaSeleccionada) && !Analizando;

    // Recarga la lista de archivos cuando cambia la carpeta
    partial void OnCarpetaSeleccionadaChanged(string value)
        => AnalizarCommand.NotifyCanExecuteChanged();

    partial void OnAnalizandoChanged(bool value)
        => AnalizarCommand.NotifyCanExecuteChanged();

    // ── Helpers ─────────────────────────────────────────────────────────

    private void CargarArchivos(string carpeta)
    {
        Archivos.Clear();
        if (!System.IO.Directory.Exists(carpeta)) return;

        var archivos = System.IO.Directory
            .EnumerateFiles(carpeta, "*", System.IO.SearchOption.AllDirectories)
            .Where(f => ExtensionesAceptadas.Contains(
                System.IO.Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f)
            .ToList();

        foreach (var archivo in archivos)
            Archivos.Add(System.IO.Path.GetRelativePath(carpeta, archivo));
    }
}
