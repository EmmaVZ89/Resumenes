using System.Diagnostics;
using System.Text;
using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.Pdf;

public class PythonGeneradorPdf(string pythonExe, string scriptPath, string fontsDir) : IGeneradorPdf
{
    public async Task GenerarAsync(string contenidoPath, string pdfPath, string titulo, string subtitulo, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(pythonExe)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,   // oculta la ventana de consola del proceso Python
            StandardOutputEncoding = Encoding.UTF8
        };
        // Pasamos el directorio de fuentes para que el script las encuentre.
        psi.Environment["RESUMENES_FONTS"] = fontsDir;

        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add(contenidoPath);
        psi.ArgumentList.Add(pdfPath);
        psi.ArgumentList.Add("--titulo"); psi.ArgumentList.Add(titulo);
        psi.ArgumentList.Add("--subtitulo"); psi.ArgumentList.Add(subtitulo);

        using var proc = Process.Start(psi)!;
        var err = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"Generación de PDF falló (exit {proc.ExitCode}): {err}");
        if (!File.Exists(pdfPath))
            throw new InvalidOperationException($"El script no produjo el PDF esperado: {pdfPath}");
    }
}
