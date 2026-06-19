using System.Diagnostics;
using System.Text;
using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.Office;

// Convierte Office -> PDF con LibreOffice headless (portable, en runtime/libreoffice/program/soffice.exe).
public class LibreOfficeConversor(string sofficePath) : IConversorOffice
{
    public async Task<string> ConvertirAPdfAsync(string archivoOffice, string outDir, CancellationToken ct)
    {
        Directory.CreateDirectory(outDir);
        // Perfil de usuario aislado: evita el choque "LibreOffice ya está en ejecución" y no toca ningún perfil del sistema.
        var perfil = Path.Combine(outDir, "_loprofile").Replace('\\', '/');

        var psi = new ProcessStartInfo(sofficePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,   // oculta la ventana de consola de LibreOffice (soffice)
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("--headless");
        psi.ArgumentList.Add("--norestore");
        psi.ArgumentList.Add($"-env:UserInstallation=file:///{perfil}");
        psi.ArgumentList.Add("--convert-to");
        psi.ArgumentList.Add("pdf");
        psi.ArgumentList.Add("--outdir");
        psi.ArgumentList.Add(outDir);
        psi.ArgumentList.Add(archivoOffice);

        // Aislar de cualquier LibreOffice/OpenOffice instalado en la máquina: si el sistema tiene
        // estas variables de entorno definidas (apuntando a OTRA instalación), nuestro soffice
        // portable cargaría el bootstrap/ure equivocado y abriría el diálogo "bootstrap.ini está
        // dañado". Las quitamos del proceso hijo para que use las suyas (relativas a soffice.exe).
        foreach (var v in new[]
                 {
                     "URE_BOOTSTRAP", "UNO_PATH", "OFFICE_HOME", "OFFICE_BASE_DIR",
                     "BRAND_BASE_DIR", "STAR_RESOURCEPATH", "UNO_TYPES", "UNO_SERVICES",
                     "PYTHONHOME", "PYTHONPATH"
                 })
        {
            psi.Environment.Remove(v);
        }

        // Timeout de seguridad: si soffice se cuelga (p.ej. abre un diálogo modal de error al
        // arrancar), no debe bloquear el pipeline indefinidamente; se mata y se lanza para que
        // el orquestador pueda recurrir al fallback.
        const int timeoutSegundos = 90;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSegundos));
        var tok = timeoutCts.Token;

        using var proc = Process.Start(psi)!;
        try
        {
            var errTask = proc.StandardError.ReadToEndAsync(tok);
            var salida = await proc.StandardOutput.ReadToEndAsync(tok);
            await proc.WaitForExitAsync(tok);
            var err = await errTask;

            var pdf = Path.Combine(outDir, Path.GetFileNameWithoutExtension(archivoOffice) + ".pdf");
            if (proc.ExitCode != 0 || !File.Exists(pdf))
                throw new InvalidOperationException(
                    $"LibreOffice no convirtió a PDF (exit {proc.ExitCode}): {err}{salida}");
            return pdf;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // No fue cancelación del usuario, sino el timeout: soffice se colgó.
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException(
                $"LibreOffice no respondió en {timeoutSegundos}s al convertir " +
                $"'{Path.GetFileName(archivoOffice)}' (posible diálogo de error al arrancar).");
        }
        catch
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            throw;
        }
    }
}
