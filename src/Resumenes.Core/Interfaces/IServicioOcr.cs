namespace Resumenes.Core.Interfaces;

public interface IServicioOcr
{
    Task<string> OcrAsync(IReadOnlyList<string> rutasImagenes, CancellationToken ct,
        IProgress<(int actual, int total)>? subProgreso = null);
}
