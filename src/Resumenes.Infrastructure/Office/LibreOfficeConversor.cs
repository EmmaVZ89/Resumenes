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

        using var proc = Process.Start(psi)!;
        try
        {
            var errTask = proc.StandardError.ReadToEndAsync(ct);
            var salida = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            var err = await errTask;

            var pdf = Path.Combine(outDir, Path.GetFileNameWithoutExtension(archivoOffice) + ".pdf");
            if (proc.ExitCode != 0 || !File.Exists(pdf))
                throw new InvalidOperationException(
                    $"LibreOffice no convirtió a PDF (exit {proc.ExitCode}): {err}{salida}");
            return pdf;
        }
        catch
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            throw;
        }
    }
}
