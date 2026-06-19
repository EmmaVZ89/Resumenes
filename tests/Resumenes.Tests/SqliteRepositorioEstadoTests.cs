using Resumenes.Core.Modelos;
using Resumenes.Infrastructure.Persistencia;
using Xunit;

namespace Resumenes.Tests;

public class SqliteRepositorioEstadoTests : IDisposable
{
    private readonly string _ruta = Path.Combine(Path.GetTempPath(), $"resu_{Guid.NewGuid():N}.sqlite");
    private SqliteRepositorioEstado Crear()
    {
        var repo = new SqliteRepositorioEstado($"Data Source={_ruta}");
        repo.InicializarEsquema();
        return repo;
    }

    [Fact]
    public void GuardaYRecupera_analisisPorFingerprint()
    {
        var repo = Crear();
        var a = new Analisis("an1", "Materia", @"C:\mat", "fp123", EstadoAnalisis.EnProceso,
            DateTime.UtcNow, DateTime.UtcNow);
        repo.GuardarAnalisis(a);

        var leido = repo.ObtenerAnalisisPorFingerprint("fp123");
        Assert.NotNull(leido);
        Assert.Equal("an1", leido!.Id);
        Assert.Equal(EstadoAnalisis.EnProceso, leido.Estado);
    }

    [Fact]
    public void GuardarUnidad_esUpsert_porClaveNatural()
    {
        var repo = Crear();
        repo.GuardarAnalisis(new Analisis("an1", "M", "c", "fp", EstadoAnalisis.EnProceso, DateTime.UtcNow, DateTime.UtcNow));
        repo.GuardarArchivo(new Archivo("arc1", "an1", "a.pdf", "a.pdf", "hash", 10, TipoArchivo.Pdf, null, DateTime.UtcNow));
        var u = new Unidad { AnalisisId = "an1", ArchivoId = "arc1", Etapa = Etapa.OcrBruto, Estado = EstadoUnidad.Pendiente, ActualizadoEn = DateTime.UtcNow };
        repo.GuardarUnidad(u);
        u.Estado = EstadoUnidad.Completado;
        u.HashEntrada = "h1";
        repo.GuardarUnidad(u);

        var leido = repo.ObtenerUnidad("an1", "arc1", null, Etapa.OcrBruto);
        Assert.NotNull(leido);
        Assert.Equal(EstadoUnidad.Completado, leido!.Estado);
        Assert.Equal("h1", leido.HashEntrada);
    }

    [Fact]
    public void GuardarTema_resuelveColisionDeOrden_reemplazandoElTemaObsoleto()
    {
        var repo = Crear();
        repo.GuardarAnalisis(new Analisis("an1", "M", "c", "fp", EstadoAnalisis.EnProceso, DateTime.UtcNow, DateTime.UtcNow));

        // Primer set: el orden 1 lo ocupa "tema-a-01".
        repo.GuardarTema(new Tema("tema-a-01", "an1", "Tema A", 1, true));
        // Re-detección: un tema NUEVO (id distinto) reutiliza el orden 1 → no debe lanzar UNIQUE.
        repo.GuardarTema(new Tema("tema-b-01", "an1", "Tema B", 1, true));

        Assert.NotNull(repo.ObtenerTema("tema-b-01"));
        Assert.Null(repo.ObtenerTema("tema-a-01")); // el obsoleto fue reemplazado
    }

    [Fact]
    public void GuardarTema_mismoId_actualizaSinReemplazar()
    {
        var repo = Crear();
        repo.GuardarAnalisis(new Analisis("an1", "M", "c", "fp", EstadoAnalisis.EnProceso, DateTime.UtcNow, DateTime.UtcNow));
        repo.GuardarTema(new Tema("t1", "an1", "Original", 1, true));
        repo.GuardarTema(new Tema("t1", "an1", "Renombrado", 1, true)); // mismo id+orden → update

        var leido = repo.ObtenerTema("t1");
        Assert.NotNull(leido);
        Assert.Equal("Renombrado", leido!.Nombre);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_ruta)) File.Delete(_ruta);
    }
}
