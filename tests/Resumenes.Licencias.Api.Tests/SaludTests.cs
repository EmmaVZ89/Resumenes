using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Resumenes.Licencias.Api.Tests;

public class SaludTests
{
    [Fact]
    public async Task Salud_DevuelveOk()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var cliente = factory.CreateClient();

        var resp = await cliente.GetAsync("/salud");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("ok", await resp.Content.ReadAsStringAsync());
    }
}
