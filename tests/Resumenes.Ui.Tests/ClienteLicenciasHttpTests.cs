using System.Net;
using System.Net.Http;
using Resumenes.Core.Licencias;
using Resumenes.Ui.Servicios.Licencia;

namespace Resumenes.Ui.Tests;

public class ClienteLicenciasHttpTests
{
    private sealed class HandlerFake(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(responder(request));
    }

    private static ClienteLicenciasHttp Cliente(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => new(new HttpClient(new HandlerFake(responder)), "https://api.test");

    [Fact]
    public async Task Activar_200ConToken_Exitoso()
    {
        var sut = Cliente(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("{\"token\":\"eyJ.abc.def\"}") });

        var r = await sut.ActivarAsync("RESU-AAAAA-BBBBB-CCCCC-DDDDD", "hw-1", "PC", default);

        Assert.True(r.Exitoso);
        Assert.Equal("eyJ.abc.def", r.Token);
    }

    [Fact]
    public async Task Activar_409LimiteAlcanzado_NoExitosoConError()
    {
        var sut = Cliente(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        { Content = new StringContent("{\"error\":\"limite_alcanzado\"}") });

        var r = await sut.ActivarAsync("RESU-AAAAA-BBBBB-CCCCC-DDDDD", "hw-1", "PC", default);

        Assert.False(r.Exitoso);
        Assert.Equal("limite_alcanzado", r.Error);
    }

    [Fact]
    public async Task Activar_SinConexion_ErrorSinConexion()
    {
        var sut = Cliente(_ => throw new HttpRequestException("boom"));
        var r = await sut.ActivarAsync("RESU-AAAAA-BBBBB-CCCCC-DDDDD", "hw-1", "PC", default);
        Assert.False(r.Exitoso);
        Assert.Equal("sin_conexion", r.Error);
    }

    [Fact]
    public async Task Validar_200_Activa()
    {
        var sut = Cliente(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("{\"estado\":\"activa\"}") });
        Assert.Equal(EstadoValidacionServidor.Activa, await sut.ValidarAsync("lic", "hw", default));
    }

    [Fact]
    public async Task Validar_403_Revocada()
    {
        var sut = Cliente(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        { Content = new StringContent("{\"estado\":\"revocada\"}") });
        Assert.Equal(EstadoValidacionServidor.Revocada, await sut.ValidarAsync("lic", "hw", default));
    }

    [Fact]
    public async Task Validar_SinConexion_SinConexion()
    {
        var sut = Cliente(_ => throw new HttpRequestException("boom"));
        Assert.Equal(EstadoValidacionServidor.SinConexion, await sut.ValidarAsync("lic", "hw", default));
    }

    [Fact]
    public async Task Validar_503ServidorNoSano_SinConexion_NoRevocada()
    {
        var sut = Cliente(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        { Content = new StringContent("Service Unavailable") });
        Assert.Equal(EstadoValidacionServidor.SinConexion, await sut.ValidarAsync("lic", "hw", default));
    }
}
