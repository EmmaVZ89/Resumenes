using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.Ocr;

/// <summary>
/// OCR vía worker PaddleOCR (proceso Python de larga vida, protocolo NDJSON).
///
/// Resiliencia (A): si el worker muere a mitad de un documento (crash nativo de PaddleOCR
/// en alguna página), NO se pierde el documento entero: la página fallida queda vacía, el
/// worker se reinicia y el OCR continúa con la página siguiente. Solo se reporta error si
/// fallan TODAS las páginas o si el worker no logra inicializarse.
///
/// Diagnóstico (D): ante cualquier falla de página se vuelca el stderr COMPLETO del worker
/// a %LOCALAPPDATA%/ResumenesApp/logs/ocr-error.log para ver el motivo nativo real del crash.
/// </summary>
public class PaddleOcrServicio(string pythonExe, string scriptPath, string modelosDir) : IServicioOcr
{
    private static readonly string RutaLogOcr = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ResumenesApp", "logs", "ocr-error.log");
    private static readonly object _bloqueoLog = new();

    public async Task<string> OcrAsync(IReadOnlyList<string> rutasImagenes, CancellationToken ct,
        IProgress<(int actual, int total)>? subProgreso = null)
    {
        var resultados = new string[rutasImagenes.Count];
        int fallidas = 0;
        Worker? worker = null;
        try
        {
            // Arranque inicial: si el worker no inicializa, el documento sí falla (problema sistémico).
            worker = await IniciarWorkerAsync(ct);

            for (int i = 0; i < rutasImagenes.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                // Reiniciar el worker si murió en la página anterior (crash nativo).
                if (worker is null || worker.Murio)
                {
                    worker?.Dispose();
                    worker = await IniciarWorkerAsync(ct);
                }

                var reqId = $"p{i + 1}";
                var r = await worker.OcrPaginaAsync(reqId, rutasImagenes[i], ct);

                if (r.Ok)
                {
                    resultados[i] = r.Texto;
                }
                else
                {
                    // Página fallida: no aborta el documento. Texto vacío + diagnóstico completo a archivo.
                    resultados[i] = "";
                    fallidas++;
                    RegistrarFallo(rutasImagenes[i], reqId, r.Error, worker.StderrCompleto());
                    // Si el worker murió, se reinicia al inicio de la próxima iteración.
                }

                subProgreso?.Report((i + 1, rutasImagenes.Count));
            }

            if (rutasImagenes.Count > 0 && fallidas == rutasImagenes.Count)
                throw new InvalidOperationException(
                    $"OCR falló en las {rutasImagenes.Count} páginas. Detalle en {RutaLogOcr}");

            return string.Join("\n\n", resultados.Where(t => !string.IsNullOrEmpty(t)));
        }
        finally
        {
            worker?.Dispose();
        }
    }

    private async Task<Worker> IniciarWorkerAsync(CancellationToken ct)
    {
        var psi = new ProcessStartInfo(pythonExe)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,   // oculta la ventana de consola del worker Python
            // UTF8Encoding(false): SIN BOM (Encoding.UTF8 emite BOM y corrompe la 1ª línea de stdin).
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardInputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add(modelosDir);

        var w = new Worker(Process.Start(psi)!);
        await w.EsperarReadyAsync(ct);
        return w;
    }

    private static void RegistrarFallo(string rutaImagen, string reqId, string? error, string stderr)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaLogOcr)!);
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] OCR falló — {reqId} — imagen: {rutaImagen}");
            sb.AppendLine($"  error: {error}");
            sb.AppendLine("  --- stderr del worker (completo) ---");
            sb.AppendLine(string.IsNullOrWhiteSpace(stderr) ? "  (vacío)" : stderr);
            sb.AppendLine("  ------------------------------------");
            sb.AppendLine();
            lock (_bloqueoLog) File.AppendAllText(RutaLogOcr, sb.ToString());
        }
        catch { /* el log de diagnóstico nunca debe tumbar el OCR */ }
    }

    private readonly record struct ResultadoPagina(bool Ok, string Texto, string? Error);

    /// <summary>Envuelve UN proceso worker de PaddleOCR y su captura de stderr.</summary>
    private sealed class Worker : IDisposable
    {
        private readonly Process _proc;
        private readonly StringBuilder _err = new();
        private readonly Task _drenajeErr;
        private bool _murio;

        public Worker(Process proc)
        {
            _proc = proc;
            // CRÍTICO: drenar stderr en paralelo. PaddleOCR escribe mucho a stderr; si no se lee,
            // el buffer del pipe se llena y el worker se bloquea (deadlock con la lectura de stdout).
            _drenajeErr = Task.Run(async () =>
            {
                string? l;
                while ((l = await _proc.StandardError.ReadLineAsync()) != null)
                    lock (_err) _err.AppendLine(l);
            });
        }

        public bool Murio => _murio || _proc.HasExited;

        public async Task EsperarReadyAsync(CancellationToken ct)
        {
            string? linea;
            while ((linea = await _proc.StandardOutput.ReadLineAsync(ct)) != null)
            {
                if (ProtocoloOcr.Parsear(linea)?.Tipo == "ready") return;
            }
            // EOF antes de "ready": el worker no logró inicializar.
            _murio = true;
            await EsperarDrenajeAsync();
            throw new InvalidOperationException(
                "El worker de OCR no pudo inicializarse. stderr (cola): " + Cola(StderrCompleto(), 800));
        }

        public async Task<ResultadoPagina> OcrPaginaAsync(string reqId, string ruta, CancellationToken ct)
        {
            // Enviar el pedido. Si el pipe está roto (worker caído), tratarlo como muerte.
            try
            {
                var pedido = JsonSerializer.Serialize(new { req_id = reqId, ruta_imagen = ruta });
                await _proc.StandardInput.WriteLineAsync(pedido);
                await _proc.StandardInput.FlushAsync(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                _murio = true;
                await EsperarDrenajeAsync();
                return new ResultadoPagina(false, "", "no se pudo enviar la página al worker de OCR (proceso caído).");
            }

            // Leer hasta el result/error de esta página, o EOF (worker muerto).
            string? linea;
            while ((linea = await _proc.StandardOutput.ReadLineAsync(ct)) != null)
            {
                var m = ProtocoloOcr.Parsear(linea);
                if (m == null || m.ReqId != reqId) continue;   // log no-JSON o de otra página
                if (m.Tipo == "result") return new ResultadoPagina(true, m.Texto ?? "", null);
                if (m.Tipo == "error") return new ResultadoPagina(false, "", m.Mensaje);
            }

            // EOF: el worker se cerró sin responder esta página (crash nativo / falta de memoria).
            _murio = true;
            try { await _proc.WaitForExitAsync(ct); } catch (OperationCanceledException) { throw; } catch { }
            await EsperarDrenajeAsync();
            return new ResultadoPagina(false, "",
                "el worker de OCR se cerró inesperadamente (posible crash nativo de PaddleOCR o falta de memoria).");
        }

        public string StderrCompleto() { lock (_err) return _err.ToString(); }

        // Esperar (acotado) a que el drenaje de stderr termine, para capturar la salida del crash.
        private async Task EsperarDrenajeAsync()
        {
            try { await _drenajeErr.WaitAsync(TimeSpan.FromSeconds(3)); } catch { }
        }

        private static string Cola(string s, int n) => s.Length <= n ? s : s[^n..];

        public void Dispose()
        {
            try
            {
                if (!_proc.HasExited)
                {
                    try { _proc.StandardInput.Close(); } catch { }
                    _proc.Kill(entireProcessTree: true);
                }
            }
            catch { }
            _proc.Dispose();
        }
    }
}
