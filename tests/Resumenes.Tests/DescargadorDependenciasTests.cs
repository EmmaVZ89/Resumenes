using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using Resumenes.Infrastructure.Instalador;
using Xunit;

namespace Resumenes.Tests;

public class DescargadorDependenciasTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), $"desc_{Guid.NewGuid():N}");

    // Handler fake: sirve un manifest y un zip; soporta Range (206) para probar reanudación.
    private sealed class FakeHandler(byte[] zip, Func<string> manifest) : HttpMessageHandler
    {
        public int VecesZip;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            if (req.RequestUri!.AbsoluteUri.EndsWith("manifest.json"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(manifest()) });

            VecesZip++;
            var range = req.Headers.Range?.Ranges.FirstOrDefault();
            if (range?.From is long from)
            {
                var resto = zip[(int)from..];
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(resto) });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(zip) });
        }
    }

    private static byte[] CrearZipConArchivo(string nombre, string contenido)
    {
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var e = z.CreateEntry(nombre);
            using var w = new StreamWriter(e.Open());
            w.Write(contenido);
        }
        return ms.ToArray();
    }

    private static string Sha256(byte[] b) => Convert.ToHexString(SHA256.HashData(b));

    private string Manifest(string sha, long bytes = 0) =>
        $$"""
        { "version":"1.0.0", "bundles":[
          { "id":"demo","url":"http://test/demo.zip","sha256":"{{sha}}","bytes":{{bytes}},"destino":"runtime/demo","tipo":"zip" }
        ]}
        """;

    [Fact]
    public async Task Descarga_verifica_y_descomprime_un_bundle()
    {
        var zip = CrearZipConArchivo("hola.txt", "mundo");
        var http = new HttpClient(new FakeHandler(zip, () => Manifest(Sha256(zip))));
        var d = new DescargadorDependencias(http, "http://test/manifest.json", _tmp);

        var r = await d.DescargarFaltantesAsync(null, CancellationToken.None);

        Assert.Equal(1, r.Ok);
        Assert.Equal(0, r.Errores);
        Assert.Equal("mundo", File.ReadAllText(Path.Combine(_tmp, "runtime", "demo", "hola.txt")));
        Assert.True(File.Exists(Path.Combine(_tmp, "runtime", "demo", ".bundle-ok")));
    }

    [Fact]
    public async Task Saltea_si_ya_esta_instalado_con_sha_valido()
    {
        var zip = CrearZipConArchivo("hola.txt", "mundo");
        var handler = new FakeHandler(zip, () => Manifest(Sha256(zip)));
        var http = new HttpClient(handler);
        var d = new DescargadorDependencias(http, "http://test/manifest.json", _tmp);

        await d.DescargarFaltantesAsync(null, CancellationToken.None);
        var r2 = await d.DescargarFaltantesAsync(null, CancellationToken.None);

        Assert.Equal(1, r2.Salteados);
        Assert.Equal(1, handler.VecesZip); // no se re-descargó
    }

    [Fact]
    public async Task Sha_invalido_reporta_error_y_no_descomprime()
    {
        var zip = CrearZipConArchivo("hola.txt", "mundo");
        var http = new HttpClient(new FakeHandler(zip, () => Manifest("00DEADBEEF")));
        var d = new DescargadorDependencias(http, "http://test/manifest.json", _tmp);

        var r = await d.DescargarFaltantesAsync(null, CancellationToken.None);

        Assert.Equal(1, r.Errores);
        Assert.False(Directory.Exists(Path.Combine(_tmp, "runtime", "demo")));
    }

    [Fact]
    public async Task Reanuda_descarga_parcial_con_Range_206()
    {
        var zip = CrearZipConArchivo("hola.txt", "mundo");
        var http = new HttpClient(new FakeHandler(zip, () => Manifest(Sha256(zip), zip.Length)));
        // Pre-crear un .part parcial (primeros 10 bytes) para forzar la reanudación por Range.
        var partPath = Path.Combine(_tmp, "runtime", "demo.part");
        Directory.CreateDirectory(Path.GetDirectoryName(partPath)!);
        await File.WriteAllBytesAsync(partPath, zip[..10]);

        var d = new DescargadorDependencias(http, "http://test/manifest.json", _tmp);
        var r = await d.DescargarFaltantesAsync(null, CancellationToken.None);

        Assert.Equal(1, r.Ok);
        Assert.Equal("mundo", File.ReadAllText(Path.Combine(_tmp, "runtime", "demo", "hola.txt")));
    }

    [Fact]
    public async Task LimpiarDestino_false_preserva_archivos_preexistentes()
    {
        // Arrange: zip con un archivo nuevo
        var zip = CrearZipConArchivo("nuevo.txt", "contenido-nuevo");
        var sha = Sha256(zip);
        var manifestJson = $$"""
        { "version":"1.0.0", "bundles":[
          { "id":"modelos","url":"http://test/modelos.zip","sha256":"{{sha}}","bytes":{{zip.Length}},"destino":"runtime/modelos","tipo":"zip","limpiarDestino":false }
        ]}
        """;
        var http = new HttpClient(new FakeHandler(zip, () => manifestJson));
        var d = new DescargadorDependencias(http, "http://test/manifest.json", _tmp);

        // Pre-crear un archivo en el destino que NO está en el zip
        var destDir = Path.Combine(_tmp, "runtime", "modelos");
        Directory.CreateDirectory(destDir);
        var preexistente = Path.Combine(destDir, "preexistente.txt");
        await File.WriteAllTextAsync(preexistente, "no-borrar");

        // Act
        var r = await d.DescargarFaltantesAsync(null, CancellationToken.None);

        // Assert: la descarga fue exitosa
        Assert.Equal(1, r.Ok);
        Assert.Equal(0, r.Errores);
        // El archivo preexistente se PRESERVA (limpiarDestino=false)
        Assert.True(File.Exists(preexistente), "El archivo preexistente debería conservarse con limpiarDestino=false");
        Assert.Equal("no-borrar", File.ReadAllText(preexistente));
        // El archivo del zip también existe
        Assert.Equal("contenido-nuevo", File.ReadAllText(Path.Combine(destDir, "nuevo.txt")));
    }

    public void Dispose() { if (Directory.Exists(_tmp)) Directory.Delete(_tmp, true); }
}
