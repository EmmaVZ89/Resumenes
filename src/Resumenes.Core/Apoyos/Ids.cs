using System.Text;
using Resumenes.Core.Modelos;

namespace Resumenes.Core.Apoyos;

public static class Ids
{
    public static string SlugId(string nombre)
    {
        var sb = new StringBuilder();
        foreach (var c in (nombre ?? "").ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        var s = sb.ToString().Trim('-');
        if (s.Length == 0) s = "analisis";
        if (s.Length > 30) s = s.Substring(0, 30);
        return s + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    public static TipoArchivo TipoDe(string ruta) => Path.GetExtension(ruta).ToLowerInvariant() switch
    {
        ".pdf" => TipoArchivo.Pdf,
        ".doc" => TipoArchivo.Doc,
        ".docx" => TipoArchivo.Docx,
        ".ppt" => TipoArchivo.Ppt,
        ".pptx" => TipoArchivo.Pptx,
        ".txt" => TipoArchivo.Txt,
        _ => TipoArchivo.Otro,
    };
}
