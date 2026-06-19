using System.Security.Cryptography;
using System.Text;

namespace Resumenes.Core.Apoyos;

public static class Hashing
{
    public static string Sha256HexDeTexto(string texto)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));

    public static string Sha256HexDeArchivo(string ruta)
    {
        using var stream = File.OpenRead(ruta);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    public static string ArchivoIdDesdeHash(string hashHex) => hashHex[..16];
}
