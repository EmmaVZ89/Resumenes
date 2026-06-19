namespace Resumenes.Core.Interfaces;

public interface IConversorOffice
{
    // Convierte un archivo Office (doc/docx/ppt/pptx) a PDF y devuelve la ruta del PDF generado.
    Task<string> ConvertirAPdfAsync(string archivoOffice, string outDir, CancellationToken ct);
}
