using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Resumenes.Core.Licencias;

namespace Resumenes.Ui.Servicios.Licencia;

public sealed class AlmacenLicenciaDpapi : IAlmacenLicencia
{
    private readonly string _ruta;

    public AlmacenLicenciaDpapi(string rutaArchivo) => _ruta = rutaArchivo;

    public DatosLicenciaGuardada? Leer()
    {
        try
        {
            if (!File.Exists(_ruta)) return null;
            var cifrado = File.ReadAllBytes(_ruta);
            var plano = ProtectedData.Unprotect(cifrado, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<DatosLicenciaGuardada>(Encoding.UTF8.GetString(plano));
        }
        catch
        {
            return null; // archivo ausente, corrupto o de otro usuario/máquina
        }
    }

    public void Guardar(DatosLicenciaGuardada datos)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_ruta)!);
        var plano = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(datos));
        var cifrado = ProtectedData.Protect(plano, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_ruta, cifrado);
    }

    public void Borrar()
    {
        if (File.Exists(_ruta)) File.Delete(_ruta);
    }
}
