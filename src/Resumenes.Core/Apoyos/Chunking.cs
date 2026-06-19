using System.Text;

namespace Resumenes.Core.Apoyos;

public static class Chunking
{
    // Divide el texto en bloques de a lo sumo maxChars, cortando en límites de línea/párrafo.
    // Si una línea sola excede maxChars, la parte de forma dura.
    public static IReadOnlyList<string> Dividir(string texto, int maxChars)
    {
        if (maxChars <= 0) throw new ArgumentOutOfRangeException(nameof(maxChars));
        if (string.IsNullOrEmpty(texto) || texto.Length <= maxChars)
            return new[] { texto ?? "" };

        var bloques = new List<string>();
        var actual = new StringBuilder();
        foreach (var linea in texto.Split('\n'))
        {
            if (actual.Length > 0 && actual.Length + linea.Length + 1 > maxChars)
            {
                bloques.Add(actual.ToString().TrimEnd('\n'));
                actual.Clear();
            }
            if (linea.Length > maxChars)
            {
                for (int i = 0; i < linea.Length; i += maxChars)
                    bloques.Add(linea.Substring(i, Math.Min(maxChars, linea.Length - i)));
            }
            else
            {
                actual.Append(linea).Append('\n');
            }
        }
        if (actual.Length > 0) bloques.Add(actual.ToString().TrimEnd('\n'));
        return bloques;
    }
}
