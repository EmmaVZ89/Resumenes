using Resumenes.Core.Interfaces;
using Resumenes.Core.Licencias;
using Resumenes.Ui.Servicios.Licencia;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Tests;

public class ActivacionVmTests
{
    private sealed class HwidFake : IServicioHwid { public string ObtenerIdEquipo() => "hw-visible"; }
    private sealed class RelojFake : IRelojUtc { public DateTime Ahora() => DateTime.UnixEpoch; }
    private sealed class AlmacenMem : IAlmacenLicencia
    {
        public DatosLicenciaGuardada? D;
        public DatosLicenciaGuardada? Leer() => D;
        public void Guardar(DatosLicenciaGuardada d) => D = d;
        public void Borrar() => D = null;
    }
    private sealed class ClienteFake(ResultadoActivacion r) : IClienteLicencias
    {
        public Task<ResultadoActivacion> ActivarAsync(string c, string h, string n, CancellationToken ct) => Task.FromResult(r);
        public Task<EstadoValidacionServidor> ValidarAsync(string l, string h, CancellationToken ct) => Task.FromResult(EstadoValidacionServidor.Activa);
    }

    private static ActivacionVm Crear(ResultadoActivacion respActivar)
    {
        var svc = new ServicioLicencia(new HwidFake(), new AlmacenMem(), new ClienteFake(respActivar),
            new ValidadorTokenLicencia(DummyPub()), new RelojFake());
        return new ActivacionVm(svc);
    }

    // Clave pública dummy (válida en formato) para construir el ValidadorTokenLicencia.
    private static string DummyPub()
    {
        using var ec = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        return Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo());
    }

    [Fact]
    public void IdEquipo_SeExpone()
    {
        var vm = Crear(new ResultadoActivacion(false, null, "clave_invalida"));
        Assert.Equal("hw-visible", vm.IdEquipo);
    }

    [Fact]
    public async Task Activar_ClaveInvalida_MuestraMensaje_NoActivada()
    {
        var vm = Crear(new ResultadoActivacion(false, null, "clave_invalida"));
        vm.Clave = "RESU-AAAAA-BBBBB-CCCCC-DDDDD";
        await vm.ActivarCommand.ExecuteAsync(null);
        Assert.False(vm.Activada);
        Assert.False(vm.Activando);
        Assert.Contains("inválida", vm.MensajeError!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Activar_LimiteAlcanzado_MensajeDeLimite()
    {
        var vm = Crear(new ResultadoActivacion(false, null, "limite_alcanzado"));
        vm.Clave = "RESU-AAAAA-BBBBB-CCCCC-DDDDD";
        await vm.ActivarCommand.ExecuteAsync(null);
        Assert.Contains("máquinas", vm.MensajeError!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Activar_SinConexion_MensajeDeConexion()
    {
        var vm = Crear(new ResultadoActivacion(false, null, "sin_conexion"));
        vm.Clave = "RESU-AAAAA-BBBBB-CCCCC-DDDDD";
        await vm.ActivarCommand.ExecuteAsync(null);
        Assert.Contains("conexión", vm.MensajeError!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Activar_ClaveVacia_NoLlamaServidor_PideClave()
    {
        var vm = Crear(new ResultadoActivacion(true, "x", null));
        vm.Clave = "   ";
        await vm.ActivarCommand.ExecuteAsync(null);
        Assert.False(vm.Activada);
        Assert.NotNull(vm.MensajeError);
    }
}
