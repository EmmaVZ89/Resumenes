using Microsoft.EntityFrameworkCore;
using Resumenes.Licencias.Api.Contratos;
using Resumenes.Licencias.Api.Datos;

namespace Resumenes.Licencias.Api.Servicios;

public class ServicioActivacion(LicenciasDbContext db, FirmadorTokens firmador)
{
    public async Task<ResultadoActivacion> ActivarAsync(string clave, string hwid, string nombreEquipo)
    {
        var lic = await db.Licencias
            .Include(l => l.Activaciones)
            .FirstOrDefaultAsync(l => l.Clave == clave);

        if (lic is null)
            return new ResultadoActivacion(CodigoActivacion.ClaveInvalida, null);
        if (lic.Estado != "activa")
            return new ResultadoActivacion(CodigoActivacion.Revocada, null);

        var ahora = DateTimeOffset.UtcNow;
        var existente = lic.Activaciones.FirstOrDefault(a => a.Hwid == hwid);
        if (existente is null)
        {
            if (lic.Activaciones.Count >= lic.MaxMaquinas)
                return new ResultadoActivacion(CodigoActivacion.LimiteAlcanzado, null);

            db.Activaciones.Add(new Activacion
            {
                Id = Guid.NewGuid(),
                LicenciaId = lic.Id,
                Hwid = hwid,
                NombreEquipo = nombreEquipo,
                PrimeraActivacion = ahora,
                UltimaValidacion = ahora,
            });
        }
        else
        {
            existente.UltimaValidacion = ahora;
            existente.NombreEquipo = nombreEquipo;
        }
        await db.SaveChangesAsync();

        var token = firmador.Firmar(lic.Id.ToString(), hwid, lic.Comprador);
        return new ResultadoActivacion(CodigoActivacion.Ok, token);
    }

    public async Task<EstadoValidacion> ValidarAsync(Guid licenciaId, string hwid)
    {
        var lic = await db.Licencias
            .Include(l => l.Activaciones)
            .FirstOrDefaultAsync(l => l.Id == licenciaId);

        if (lic is null || lic.Estado != "activa")
            return EstadoValidacion.Revocada;

        var act = lic.Activaciones.FirstOrDefault(a => a.Hwid == hwid);
        if (act is null)
            return EstadoValidacion.Revocada;

        act.UltimaValidacion = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return EstadoValidacion.Activa;
    }
}
