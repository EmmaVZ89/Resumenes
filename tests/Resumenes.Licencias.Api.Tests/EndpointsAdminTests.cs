using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Resumenes.Licencias.Api.Contratos;
using Resumenes.Licencias.Api.Datos;

namespace Resumenes.Licencias.Api.Tests;

public class EndpointsAdminTests
{
    private static FabricaApiPruebas CrearFabrica()
    {
        var f = new FabricaApiPruebas();
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        f.EstablecerFirma(ec.ExportECPrivateKeyPem());
        return f;
    }

    private static HttpClient ClienteAdmin(FabricaApiPruebas f)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Add("X-Admin-Key", f.AdminKey);
        return c;
    }

    [Fact]
    public async Task SinAdminKey_401()
    {
        await using var f = CrearFabrica();
        var c = f.CreateClient();

        var resp = await c.PostAsJsonAsync("/admin/licencias",
            new CrearLicenciaRequest("Juan", "j@x.com", null));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task CrearLicencia_DevuelveClaveValida_YPersiste()
    {
        await using var f = CrearFabrica();
        var c = ClienteAdmin(f);

        var resp = await c.PostAsJsonAsync("/admin/licencias",
            new CrearLicenciaRequest("Juan", "j@x.com", null));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var cuerpo = await resp.Content.ReadFromJsonAsync<CrearLicenciaResponse>();
        Assert.StartsWith("RESU-", cuerpo!.Clave);
        Assert.Equal(2, cuerpo.MaxMaquinas); // default

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LicenciasDbContext>();
        Assert.Equal(1, db.Licencias.Count());
    }

    [Fact]
    public async Task Revocar_CambiaEstadoYValidarDevuelve403()
    {
        await using var f = CrearFabrica();
        var c = ClienteAdmin(f);
        var creada = await (await c.PostAsJsonAsync("/admin/licencias",
            new CrearLicenciaRequest("Juan", "j@x.com", 2)))
            .Content.ReadFromJsonAsync<CrearLicenciaResponse>();
        await c.PostAsJsonAsync("/activar", new ActivarRequest(creada!.Clave, "hw-1", "PC"));

        var resp = await c.PostAsync($"/admin/licencias/{creada.Id}/revocar", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var val = await c.PostAsJsonAsync("/validar",
            new ValidarRequest(creada.Id.ToString(), "hw-1"));
        Assert.Equal(HttpStatusCode.Forbidden, val.StatusCode);
    }

    [Fact]
    public async Task LiberarActivacion_PermiteActivarOtraMaquina()
    {
        await using var f = CrearFabrica();
        var c = ClienteAdmin(f);
        var creada = await (await c.PostAsJsonAsync("/admin/licencias",
            new CrearLicenciaRequest("Juan", "j@x.com", 1)))
            .Content.ReadFromJsonAsync<CrearLicenciaResponse>();
        await c.PostAsJsonAsync("/activar", new ActivarRequest(creada!.Clave, "hw-1", "PC1"));

        // ubicar el id de la activación
        Guid actId;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LicenciasDbContext>();
            actId = db.Activaciones.Single().Id;
        }

        var del = await c.DeleteAsync($"/admin/activaciones/{actId}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        var reactivar = await c.PostAsJsonAsync("/activar",
            new ActivarRequest(creada.Clave, "hw-2", "PC2"));
        Assert.Equal(HttpStatusCode.OK, reactivar.StatusCode);
    }
}
