namespace Resumenes.Core.Interfaces;

public interface IRasterizador
{
    Task<IReadOnlyList<string>> RasterizarAsync(string pdfPath, string outDir, int dpi, CancellationToken ct,
        IProgress<(int actual, int total)>? subProgreso = null);
}
