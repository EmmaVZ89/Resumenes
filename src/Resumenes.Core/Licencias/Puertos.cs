namespace Resumenes.Core.Licencias;

public interface IServicioHwid
{
    string ObtenerIdEquipo();
}

public interface IAlmacenLicencia
{
    DatosLicenciaGuardada? Leer();
    void Guardar(DatosLicenciaGuardada datos);
    void Borrar();
}

public interface IClienteLicencias
{
    Task<ResultadoActivacion> ActivarAsync(string clave, string hwid, string nombreEquipo, CancellationToken ct);
    Task<EstadoValidacionServidor> ValidarAsync(string licenciaId, string hwid, CancellationToken ct);
}
