namespace Resumenes.Core.Interfaces;

public interface IGeneradorPdf
{
    Task GenerarAsync(string contenidoPath, string pdfPath, string titulo, string subtitulo, CancellationToken ct);
}
