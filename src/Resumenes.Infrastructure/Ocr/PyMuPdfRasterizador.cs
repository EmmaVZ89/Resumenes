using System.Diagnostics;
using System.Text;
using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.Ocr;

public class PyMuPdfRasterizador(string pythonExe, string scriptPath) : IRasterizador
{
    public async Task<IReadOnlyList<string>> RasterizarAsync(string pdfPath, string outDir, int dpi, CancellationToken ct,
        IProgress<(int actual, int total)>? subProgreso = null)
    {
        var psi = new ProcessStartInfo(pythonExe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,   // oculta la ventana de consola del proceso Python
            StandardOutputEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add(pdfPath);
        psi.ArgumentList.Add(outDir);
        psi.ArgumentList.Add(dpi.ToString());

        using var proc = Process.Start(psi)!;
        var salida = await proc.StandardOutput.ReadToEndAsync(ct);
        var err = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"Rasterizado falló (exit {proc.ExitCode}): {err}");

        var rutas = salida.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        subProgreso?.Report((rutas.Length, rutas.Length));
        return rutas;
    }
}
