using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Resumenes.Licencias.Api.Contratos;
using Resumenes.Licencias.Api.Datos;
using Resumenes.Licencias.Api.Servicios;

namespace Resumenes.Licencias.Api.Endpoints;

public static class EndpointsAdmin
{
    public static void Mapear(WebApplication app)
    {
        var grupo = app.MapGroup("/admin");
        grupo.AddEndpointFilter(async (ctx, next) =>
        {
            var enviada = ctx.HttpContext.Request.Headers["X-Admin-Key"].ToString();
            var esperada = Environment.GetEnvironmentVariable("ADMIN_KEY") ?? "";
            if (!ClaveAdminValida(enviada, esperada))
                return Results.Unauthorized();
            return await next(ctx);
        });

        grupo.MapPost("/licencias", async (CrearLicenciaRequest req, LicenciasDbContext db) =>
        {
            var lic = new Licencia
            {
                Id = Guid.NewGuid(),
                Clave = GeneradorClaves.Generar(),
                Comprador = req.Comprador,
                Email = req.Email,
                MaxMaquinas = req.MaxMaquinas is > 0 ? req.MaxMaquinas.Value : 2,
                Estado = "activa",
                CreadaEn = DateTimeOffset.UtcNow,
            };
            db.Licencias.Add(lic);
            await db.SaveChangesAsync();
            return Results.Ok(new CrearLicenciaResponse(lic.Id, lic.Clave, lic.Comprador, lic.MaxMaquinas));
        });

        grupo.MapGet("/licencias", async (LicenciasDbContext db) =>
            Results.Ok(await db.Licencias.Include(l => l.Activaciones).ToListAsync()));

        grupo.MapPost("/licencias/{id:guid}/revocar", async (Guid id, LicenciasDbContext db) =>
        {
            var lic = await db.Licencias.FindAsync(id);
            if (lic is null) return Results.NotFound();
            lic.Estado = "revocada";
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        grupo.MapDelete("/activaciones/{id:guid}", async (Guid id, LicenciasDbContext db) =>
        {
            var act = await db.Activaciones.FindAsync(id);
            if (act is null) return Results.NotFound();
            db.Activaciones.Remove(act);
            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }

    private static bool ClaveAdminValida(string enviada, string esperada)
    {
        if (string.IsNullOrEmpty(esperada) || string.IsNullOrEmpty(enviada))
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(enviada), Encoding.UTF8.GetBytes(esperada));
    }
}
