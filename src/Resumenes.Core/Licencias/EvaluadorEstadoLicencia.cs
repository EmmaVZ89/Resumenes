namespace Resumenes.Core.Licencias;

public static class EvaluadorEstadoLicencia
{
    public static readonly TimeSpan IntervaloRevalidacion = TimeSpan.FromDays(14);
    public static readonly TimeSpan Gracia = TimeSpan.FromDays(30);

    public static EstadoLicenciaCliente Evaluar(
        bool tieneTokenValido, DateTime? ultimaValidacion, DateTime ahora)
    {
        if (!tieneTokenValido) return EstadoLicenciaCliente.SinLicencia;
        if (ultimaValidacion is null) return EstadoLicenciaCliente.RevalidarAhora;

        var transcurrido = ahora - ultimaValidacion.Value;
        if (transcurrido > Gracia) return EstadoLicenciaCliente.BloqueadaPorGracia;
        if (transcurrido > IntervaloRevalidacion) return EstadoLicenciaCliente.RevalidarAhora;
        return EstadoLicenciaCliente.Activa;
    }
}
