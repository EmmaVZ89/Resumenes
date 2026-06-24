using System.IO;
using Resumenes.Core.Licencias;
using Resumenes.Ui.Servicios.Licencia;

namespace Resumenes.Ui.Tests;

public class AlmacenLicenciaDpapiTests : IDisposable
{
    private readonly string _ruta = Path.Combine(Path.GetTempPath(), $"lic-test-{Guid.NewGuid():N}.dat");

    [Fact]
    public void GuardarYLeer_DevuelveLosMismosDatos()
    {
        var sut = new AlmacenLicenciaDpapi(_ruta);
        var datos = new DatosLicenciaGuardada("token-xyz", new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc));

        sut.Guardar(datos);
        var leido = sut.Leer();

        Assert.NotNull(leido);
        Assert.Equal("token-xyz", leido!.Token);
        Assert.Equal(datos.UltimaValidacionExitosa, leido.UltimaValidacionExitosa);
    }

    [Fact]
    public void Leer_SinArchivo_DevuelveNull()
        => Assert.Null(new AlmacenLicenciaDpapi(_ruta).Leer());

    [Fact]
    public void Borrar_EliminaElArchivo_YLeerDevuelveNull()
    {
        var sut = new AlmacenLicenciaDpapi(_ruta);
        sut.Guardar(new DatosLicenciaGuardada("t", DateTime.UtcNow));
        sut.Borrar();
        Assert.Null(sut.Leer());
    }

    [Fact]
    public void Leer_ArchivoCorrupto_DevuelveNull()
    {
        File.WriteAllText(_ruta, "esto no es DPAPI valido");
        Assert.Null(new AlmacenLicenciaDpapi(_ruta).Leer());
    }

    public void Dispose() { if (File.Exists(_ruta)) File.Delete(_ruta); }
}
