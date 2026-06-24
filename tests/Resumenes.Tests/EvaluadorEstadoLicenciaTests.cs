using Resumenes.Core.Licencias;

namespace Resumenes.Tests;

public class EvaluadorEstadoLicenciaTests
{
    private static readonly DateTime Ahora = new(2026, 6, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SinTokenValido_DevuelveSinLicencia()
        => Assert.Equal(EstadoLicenciaCliente.SinLicencia,
            EvaluadorEstadoLicencia.Evaluar(false, Ahora.AddDays(-1), Ahora));

    [Fact]
    public void ValidadoHace5Dias_DevuelveActiva()
        => Assert.Equal(EstadoLicenciaCliente.Activa,
            EvaluadorEstadoLicencia.Evaluar(true, Ahora.AddDays(-5), Ahora));

    [Fact]
    public void ValidadoHace14DiasExactos_DevuelveActiva()
        => Assert.Equal(EstadoLicenciaCliente.Activa,
            EvaluadorEstadoLicencia.Evaluar(true, Ahora.AddDays(-14), Ahora));

    [Fact]
    public void ValidadoHace15Dias_DevuelveRevalidarAhora()
        => Assert.Equal(EstadoLicenciaCliente.RevalidarAhora,
            EvaluadorEstadoLicencia.Evaluar(true, Ahora.AddDays(-15), Ahora));

    [Fact]
    public void ValidadoHace30DiasExactos_DevuelveRevalidarAhora()
        => Assert.Equal(EstadoLicenciaCliente.RevalidarAhora,
            EvaluadorEstadoLicencia.Evaluar(true, Ahora.AddDays(-30), Ahora));

    [Fact]
    public void ValidadoHace31Dias_DevuelveBloqueadaPorGracia()
        => Assert.Equal(EstadoLicenciaCliente.BloqueadaPorGracia,
            EvaluadorEstadoLicencia.Evaluar(true, Ahora.AddDays(-31), Ahora));

    [Fact]
    public void TokenValidoSinFechaPrevia_DevuelveRevalidarAhora()
        => Assert.Equal(EstadoLicenciaCliente.RevalidarAhora,
            EvaluadorEstadoLicencia.Evaluar(true, null, Ahora));
}
