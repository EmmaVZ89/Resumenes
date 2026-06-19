using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Resumenes.Core.Interfaces;
using Resumenes.Infrastructure.Aplicacion;
using Resumenes.Ui.Servicios;
using Resumenes.Ui.Vistas;

namespace Resumenes.Ui.ViewModels;

/// <summary>
/// Representa un requisito del sistema con su estado de verificación.
/// </summary>
public partial class RequisitoVm : ObservableObject
{
    [ObservableProperty] private string _nombre = string.Empty;
    [ObservableProperty] private string _descripcion = string.Empty;
    [ObservableProperty] private bool _cumplido;
    [ObservableProperty] private string _guia = string.Empty;

    public string Icono => Cumplido ? "CheckmarkCircle24" : "DismissCircle24";
}

/// <summary>
/// ViewModel de la pantalla de Onboarding (primer arranque).
/// Verifica la presencia del runtime (scripts, fuentes, soffice, API key) y
/// guía al usuario para completar la configuración mínima.
/// La descarga de dependencias se conecta en el sub-proyecto Instalador;
/// acá se expone un placeholder deshabilitado.
/// </summary>
public partial class OnboardingVm : VistaModeloBase
{
    private readonly IAlmacenSecretos _secretos;
    private readonly Configuracion _cfg;
    private readonly ServicioNavegacion _nav;
    private readonly IDescargadorDependencias _descargador;

    public ObservableCollection<RequisitoVm> Requisitos { get; } = new();

    /// <summary>True cuando al menos la API key está presente (mínimo para usar la app).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IrAInicioCommand))]
    private bool _esencialListo;

    /// <summary>True cuando todos los requisitos están cumplidos.</summary>
    [ObservableProperty]
    private bool _todoListo;

    /// <summary>Texto guía general para el usuario.</summary>
    [ObservableProperty]
    private string _mensajeGuia = string.Empty;

    /// <summary>True mientras se ejecuta la descarga de dependencias.</summary>
    [ObservableProperty]
    private bool _descargando;

    /// <summary>Texto de progreso del bundle en curso.</summary>
    [ObservableProperty]
    private string _textoProgreso = string.Empty;

    /// <summary>Fracción global de la descarga (0.0 a 1.0).</summary>
    [ObservableProperty]
    private double _fraccionGlobal;

    public OnboardingVm(IAlmacenSecretos secretos, Configuracion cfg, ServicioNavegacion nav,
        IDescargadorDependencias descargador)
    {
        _secretos = secretos;
        _cfg = cfg;
        _nav = nav;
        _descargador = descargador;
        Verificar();
    }

    /// <summary>
    /// Verifica el estado de cada requisito del runtime y actualiza la colección.
    /// </summary>
    [RelayCommand]
    public void Verificar()
    {
        Requisitos.Clear();

        var raizApp = AppContext.BaseDirectory;

        // Resolver rutas (mismo patrón que App.xaml.cs)
        string Resolver(string r)
        {
            if (System.IO.Path.IsPathRooted(r)) return r;
            var juntoExe = System.IO.Path.Combine(raizApp, r);
            if (System.IO.Directory.Exists(juntoExe) || System.IO.File.Exists(juntoExe)) return juntoExe;
            var juntoCwd = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), r);
            if (System.IO.Directory.Exists(juntoCwd) || System.IO.File.Exists(juntoCwd)) return juntoCwd;
            return juntoExe;
        }

        var scriptsDir = Resolver(_cfg.ScriptsDir);
        var fontsDir = Resolver(_cfg.FontsDir);
        var modelsDir = Resolver(_cfg.ModelosPaddle);
        var sofficePath = System.IO.Path.Combine(Resolver(_cfg.LibreOfficeDir), "program", "soffice.exe");

        // ── Requisito 1: API Key ────────────────────────────────────────
        var apiKeyOk = _secretos.ObtenerApiKey() is not null;
        Requisitos.Add(new RequisitoVm
        {
            Nombre = "API Key de Deepseek",
            Descripcion = apiKeyOk ? "API key guardada correctamente." : "No se encontró la API key.",
            Cumplido = apiKeyOk,
            Guia = apiKeyOk ? "" : "Ve a Configuración → API Key e ingresá tu clave de Deepseek."
        });

        // ── Requisito 2: Script rasterizar.py ──────────────────────────
        var rasterPy = System.IO.Path.Combine(scriptsDir, "rasterizar.py");
        var rasterOk = System.IO.File.Exists(rasterPy);
        Requisitos.Add(new RequisitoVm
        {
            Nombre = "Script rasterizar.py",
            Descripcion = rasterOk ? $"Encontrado en {rasterPy}" : $"No encontrado en {rasterPy}",
            Cumplido = rasterOk,
            Guia = rasterOk ? "" : "Instalá el runtime usando el Instalador de la aplicación."
        });

        // ── Requisito 3: Script worker_ocr.py ──────────────────────────
        var ocrPy = System.IO.Path.Combine(scriptsDir, "worker_ocr.py");
        var ocrOk = System.IO.File.Exists(ocrPy);
        Requisitos.Add(new RequisitoVm
        {
            Nombre = "Script worker_ocr.py",
            Descripcion = ocrOk ? $"Encontrado en {ocrPy}" : $"No encontrado en {ocrPy}",
            Cumplido = ocrOk,
            Guia = ocrOk ? "" : "Instalá el runtime usando el Instalador de la aplicación."
        });

        // ── Requisito 4: Script generador_estudio_final.py ─────────────
        var genPy = System.IO.Path.Combine(scriptsDir, "generador_estudio_final.py");
        var genOk = System.IO.File.Exists(genPy);
        Requisitos.Add(new RequisitoVm
        {
            Nombre = "Script generador_estudio_final.py",
            Descripcion = genOk ? $"Encontrado en {genPy}" : $"No encontrado en {genPy}",
            Cumplido = genOk,
            Guia = genOk ? "" : "Instalá el runtime usando el Instalador de la aplicación."
        });

        // ── Requisito 5: Fuentes DejaVu ────────────────────────────────
        var dejaVu = System.IO.Path.Combine(fontsDir, "DejaVuSans.ttf");
        var fontOk = System.IO.File.Exists(dejaVu);
        Requisitos.Add(new RequisitoVm
        {
            Nombre = "Fuentes DejaVu",
            Descripcion = fontOk ? $"Encontradas en {fontsDir}" : $"No encontradas en {fontsDir}",
            Cumplido = fontOk,
            Guia = fontOk ? "" : "Instalá el runtime usando el Instalador de la aplicación."
        });

        // ── Requisito 6: LibreOffice (soffice) ─────────────────────────
        var sofficeOk = System.IO.File.Exists(sofficePath);
        Requisitos.Add(new RequisitoVm
        {
            Nombre = "LibreOffice (soffice.exe)",
            Descripcion = sofficeOk ? $"Encontrado en {sofficePath}" : $"No encontrado en {sofficePath}",
            Cumplido = sofficeOk,
            Guia = sofficeOk ? "" : "Instalá el runtime usando el Instalador de la aplicación."
        });

        // ── Requisito 7: Modelos PaddleOCR ────────────────────────────
        var modelosOk = System.IO.Directory.Exists(modelsDir) &&
                        System.IO.Directory.EnumerateFileSystemEntries(modelsDir).Any();
        Requisitos.Add(new RequisitoVm
        {
            Nombre = "Modelos de OCR (PaddleOCR)",
            Descripcion = modelosOk ? $"Encontrados en {modelsDir}" : $"No encontrados en {modelsDir}",
            Cumplido = modelosOk,
            Guia = modelosOk ? "" : "Instalá el runtime usando el Instalador de la aplicación."
        });

        // ── Requisito 8: Carpeta de salida ─────────────────────────────
        var wsOk = !string.IsNullOrWhiteSpace(_cfg.RutaWorkspace);
        Requisitos.Add(new RequisitoVm
        {
            Nombre = "Carpeta de salida",
            Descripcion = wsOk ? $"Configurada: {_cfg.RutaWorkspace}" : "No hay carpeta de salida configurada.",
            Cumplido = wsOk,
            Guia = wsOk ? "" : "Ve a Configuración y especificá la carpeta de salida (workspace)."
        });

        // ── Actualizar estado global ────────────────────────────────────
        EsencialListo = apiKeyOk; // mínimo para usar la app
        TodoListo = Requisitos.All(r => r.Cumplido);

        MensajeGuia = TodoListo
            ? "Todo listo. La aplicación está lista para usar."
            : !apiKeyOk
                ? "Primero configurá la API Key de Deepseek para poder usar la aplicación."
                : "Podés usar la app, pero algunas funciones requieren el runtime completo.";
    }

    /// <summary>
    /// Navega a VistaInicio. Solo habilitado si lo esencial (API key) está presente.
    /// </summary>
    [RelayCommand(CanExecute = nameof(EsencialListo))]
    private void IrAInicio()
    {
        _nav.Navegar<VistaInicio>();
    }

    /// <summary>
    /// Navega a VistaConfiguracion para que el usuario cargue la API key y la carpeta.
    /// </summary>
    [RelayCommand]
    private void IrAConfiguracion()
    {
        _nav.Navegar<VistaConfiguracion>();
    }

    /// <summary>Descarga las dependencias faltantes mostrando progreso; al terminar re-verifica.</summary>
    [RelayCommand]
    private async Task DescargarDependencias()
    {
        if (Descargando) return;
        Descargando = true;
        TextoProgreso = "Iniciando…";
        FraccionGlobal = 0;
        try
        {
            var progreso = new Progress<EstadoDescarga>(e =>
            {
                TextoProgreso = $"{e.BundleId} — {e.Detalle}";
                if (e.BundleTotal > 0)
                {
                    double fracBundle = e.BytesTotal > 0
                        ? (double)e.BytesActual / e.BytesTotal
                        : (e.Fase == FaseDescarga.Completado ? 1 : 0);
                    FraccionGlobal = ((e.BundleIndice - 1) + fracBundle) / e.BundleTotal;
                }
            });
            // Offload del trabajo pesado (descarga + SHA-256 + descompresión de ~GB) a un hilo
            // de fondo para no congelar la UI; Progress<T> marshalea los avances al hilo de UI.
            var r = await Task.Run(() => _descargador.DescargarFaltantesAsync(progreso, CancellationToken.None));
            FraccionGlobal = 1.0;
            TextoProgreso = r.Errores == 0
                ? $"Descarga completa — 100% ({r.Ok} listo(s), {r.Salteados} ya estaban)."
                : $"Con {r.Errores} error(es): {string.Join(" | ", r.Fallos)}";
            Verificar();
        }
        catch (System.Exception ex)
        {
            TextoProgreso = $"Error: {ex.Message}";
        }
        finally
        {
            Descargando = false;
        }
    }

}
