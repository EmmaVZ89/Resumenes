using Resumenes.Core.Modelos;
using Resumenes.Tests.Fakes;
using Xunit;

namespace Resumenes.Tests;

public class ExclusionArchivosTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), $"resu-excl-{Guid.NewGuid():N}");

    private string CarpetaConDos()
    {
        var carpeta = Path.Combine(_base, "material");
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(Path.Combine(carpeta, "a.txt"), "contenido a");
        File.WriteAllText(Path.Combine(carpeta, "b.txt"), "contenido b");
        return carpeta;
    }

    [Fact]
    public void ListarArchivosCandidatos_DevuelveLosDosTxt()
    {
        var carpeta = CarpetaConDos();
        var svc = ServicioAnalisisFactory.ParaTests(new RepositorioEnMemoria(), Path.Combine(_base, "ws"));
        var candidatos = svc.ListarArchivosCandidatos(carpeta);
        Assert.Equal(new[] { "a.txt", "b.txt" }, candidatos.OrderBy(x => x));
    }

    [Fact]
    public async Task AbrirOCrear_ConExclusion_ProcesaSoloIncluidos_yPersiste()
    {
        var carpeta = CarpetaConDos();
        var repo = new RepositorioEnMemoria();
        var svc = ServicioAnalisisFactory.ParaTests(repo, Path.Combine(_base, "ws"));

        var an = await svc.AbrirOCrearAsync(carpeta, default, new[] { "b.txt" });
        await svc.ProcesarArchivosAsync(an, null, default);

        // Solo se registró/procesó a.txt (b.txt excluido)
        Assert.Equal(new[] { "b.txt" }, repo.ObtenerExclusiones(Path.GetFullPath(carpeta)));

        // Reanudar SIN pasar exclusiones: debe leer las persistidas y dar el MISMO análisis
        var an2 = await svc.AbrirOCrearAsync(carpeta, default);
        Assert.Equal(an.Id, an2.Id);   // mismo fingerprint ⇒ mismo análisis (no duplica)
    }

    public void Dispose()
    {
        if (Directory.Exists(_base)) Directory.Delete(_base, true);
    }
}
