using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Resumenes.Core.Interfaces;
using Resumenes.Infrastructure.Aplicacion;
using Wpf.Ui.Appearance;

namespace Resumenes.Ui.ViewModels;

/// <summary>
/// ViewModel de la pantalla de Configuración de la aplicación.
/// Permite editar la API key (DPAPI), carpeta de salida, DPI, modelo y tema oscuro/claro.
/// </summary>
public partial class ConfiguracionVm : VistaModeloBase
{
    private readonly IAlmacenSecretos _secretos;
    private readonly Configuracion _cfg;

    // ── API key ──────────────────────────────────────────────────────────
    /// <summary>Texto ingresado para la nueva API key (nunca se muestra la guardada).</summary>
    [ObservableProperty]
    private string _apiKey = string.Empty;

    /// <summary>True si ya existe una key guardada en el almacén DPAPI.</summary>
    [ObservableProperty]
    private bool _tieneKey;

    // ── Configuración de análisis ────────────────────────────────────────
    [ObservableProperty]
    private string _carpetaSalida = string.Empty;

    [ObservableProperty]
    private int _dpi;

    [ObservableProperty]
    private string _modelo = string.Empty;

    // ── Tema ─────────────────────────────────────────────────────────────
    [ObservableProperty]
    private bool _temaOscuro;

    // ── Feedback ─────────────────────────────────────────────────────────
    [ObservableProperty]
    private string _mensajeEstado = string.Empty;

    public ConfiguracionVm(IAlmacenSecretos secretos, Configuracion cfg)
    {
        _secretos = secretos;
        _cfg = cfg;
        Cargar();
    }

    /// <summary>Carga los valores actuales de la configuración.</summary>
    public void Cargar()
    {
        TieneKey = _secretos.ObtenerApiKey() is not null;
        ApiKey = string.Empty; // nunca mostrar la key en claro

        CarpetaSalida = _cfg.RutaWorkspace;
        Dpi = _cfg.Dpi;
        Modelo = _cfg.Modelo;

        // Detectar tema actual
        TemaOscuro = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;
    }

    // ── Comandos ────────────────────────────────────────────────────────

    /// <summary>Guarda la API key ingresada en el almacén DPAPI.</summary>
    [RelayCommand(CanExecute = nameof(PuedeGuardarKey))]
    private void GuardarApiKey()
    {
        if (string.IsNullOrWhiteSpace(ApiKey)) return;
        try
        {
            _secretos.GuardarApiKey(ApiKey.Trim());
            TieneKey = true;
            ApiKey = string.Empty;
            MensajeEstado = "API key guardada correctamente.";
        }
        catch (Exception ex)
        {
            MensajeEstado = $"Error al guardar la API key: {ex.Message}";
        }
    }

    private bool PuedeGuardarKey() => !string.IsNullOrWhiteSpace(ApiKey);

    partial void OnApiKeyChanged(string value) => GuardarApiKeyCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// Serializa la configuración actual a &lt;exe&gt;/config/settings.json.
    /// </summary>
    [RelayCommand]
    private void GuardarConfig()
    {
        try
        {
            _cfg.RutaWorkspace = CarpetaSalida.Trim();
            _cfg.Dpi = Dpi;
            _cfg.Modelo = Modelo.Trim();

            var raizApp = AppContext.BaseDirectory;
            var cfgDir = Path.Combine(raizApp, "config");
            Directory.CreateDirectory(cfgDir);
            var cfgPath = Path.Combine(cfgDir, "settings.json");

            var json = JsonSerializer.Serialize(_cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(cfgPath, json);
            MensajeEstado = "Configuración guardada correctamente.";
        }
        catch (Exception ex)
        {
            MensajeEstado = $"Error al guardar configuración: {ex.Message}";
        }
    }

    /// <summary>Aplica el tema oscuro o claro usando ApplicationThemeManager de WPF-UI 4.x.</summary>
    [RelayCommand]
    private void AplicarTema()
    {
        var tema = TemaOscuro ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(tema);
        MensajeEstado = $"Tema {(TemaOscuro ? "oscuro" : "claro")} aplicado.";
    }
}
