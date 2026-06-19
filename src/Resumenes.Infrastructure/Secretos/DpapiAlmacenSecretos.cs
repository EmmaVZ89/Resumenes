using System.Security.Cryptography;
using System.Text;
using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.Secretos;

// Cifra la API key con DPAPI (scope CurrentUser). Solo funciona en Windows.
public class DpapiAlmacenSecretos(string rutaArchivo) : IAlmacenSecretos
{
    public void GuardarApiKey(string key)
    {
        var cifrado = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser);
        var dir = Path.GetDirectoryName(rutaArchivo);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(rutaArchivo, cifrado);
    }

    public string? ObtenerApiKey()
    {
        if (!File.Exists(rutaArchivo)) return null;
        var cifrado = File.ReadAllBytes(rutaArchivo);
        var datos = ProtectedData.Unprotect(cifrado, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(datos);
    }
}
