using System.Net;
using System.Text;
using Resumenes.Core.Interfaces;
using Resumenes.Infrastructure.IA;
using Xunit;

namespace Resumenes.Tests;

public class DeepseekClienteIATests
{
    private sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> fn) : HttpMessageHandler
    {
        public HttpRequestMessage? UltimaPeticion;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UltimaPeticion = request;
            if (request.Content != null) await request.Content.LoadIntoBufferAsync();
            return fn(request);
        }
    }

    private sealed class SecretosFijo(string? key) : IAlmacenSecretos
    {
        public void GuardarApiKey(string k) { }
        public string? ObtenerApiKey() => key;
    }

    [Fact]
    public async Task Completar_parseaContenidoUsageYFinishReason()
    {
        var json = """
        {"choices":[{"message":{"content":"texto limpio"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}
        """;
        var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var cliente = new DeepseekClienteIA(new HttpClient(handler), new SecretosFijo("k-123"), "https://api.deepseek.com");

        var r = await cliente.CompletarAsync(
            new SolicitudIA("sys", "user", 0.2, 1000, "limpieza-v1", "deepseek-chat"), default);

        Assert.Equal("texto limpio", r.Texto);
        Assert.Equal("stop", r.FinishReason);
        Assert.Equal(15, r.TokensTotal);
        Assert.Equal("Bearer k-123", handler.UltimaPeticion!.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task Completar_sinApiKey_lanzaInvalidOperation()
    {
        var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var cliente = new DeepseekClienteIA(new HttpClient(handler), new SecretosFijo(null), "https://api.deepseek.com");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cliente.CompletarAsync(new SolicitudIA("s", "u", 0.2, 100, "v1", "deepseek-chat"), default));
    }
}
