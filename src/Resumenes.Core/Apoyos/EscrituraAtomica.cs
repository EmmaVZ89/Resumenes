using System.Text;

namespace Resumenes.Core.Apoyos;

public static class EscrituraAtomica
{
    private static readonly UTF8Encoding Utf8SinBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void Escribir(string ruta, string contenido)
        => EscribirBytes(ruta, Utf8SinBom.GetBytes(contenido));

    public static void EscribirBytes(string ruta, byte[] datos)
    {
        var dir = Path.GetDirectoryName(ruta);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = ruta + ".tmp";
        File.WriteAllBytes(tmp, datos);
        // File.Move con overwrite usa MoveFileEx (reemplazo atómico en el mismo volumen).
        File.Move(tmp, ruta, overwrite: true);
    }
}
