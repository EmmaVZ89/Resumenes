using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Resumenes.Licencias.Api.Contratos;

namespace Resumenes.Licencias.Api.Tests;

public class SuiteCompletaTests
{
    [Fact]
    public async Task FlujoCompleto_CrearActivarValidarRevocar()
    {
        await using var f = new FabricaApiPruebas();
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        f.EstablecerFirma(ec.ExportECPrivateKeyPem());

        var admin = f.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Admin-Key", f.AdminKey);
        var app = f.CreateClient();

        var creada = await (await admin.PostAsJsonAsync("/admin/licencias",
            new CrearLicenciaRequest("Juan", "j@x.com", 2)))
            .Content.ReadFromJsonAsync<CrearLicenciaResponse>();

        var act = await app.PostAsJsonAsync("/activar",
            new ActivarRequest(creada!.Clave, "hw-1", "PC"));
        Assert.Equal(HttpStatusCode.OK, act.StatusCode);

        var val = await app.PostAsJsonAsync("/validar",
            new ValidarRequest(creada.Id.ToString(), "hw-1"));
        Assert.Equal(HttpStatusCode.OK, val.StatusCode);

        await admin.PostAsync($"/admin/licencias/{creada.Id}/revocar", null);

        var val2 = await app.PostAsJsonAsync("/validar",
            new ValidarRequest(creada.Id.ToString(), "hw-1"));
        Assert.Equal(HttpStatusCode.Forbidden, val2.StatusCode);
    }
}
