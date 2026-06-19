using Resumenes.Core.Modelos;
using Resumenes.Infrastructure.Persistencia;
using Xunit;

namespace Resumenes.Tests;

public class ListarAnalisisTests : IDisposable
{
    private readonly string _ruta = Path.Combine(Path.GetTempPath(), $"resu_{Guid.NewGuid():N}.sqlite");
    private SqliteRepositorioEstado Crear()
    {
        var repo = new SqliteRepositorioEstado($"Data Source={_ruta}");
        repo.InicializarEsquema();
        return repo;
    }

    [Fact]
    public void Lista_analisis_ordenados_por_actualizado_desc()
    {
        var repo = Crear();
        repo.GuardarAnalisis(new Analisis("a1", "Uno", "c1", "fp1", EstadoAnalisis.Completado,
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 1)));
        repo.GuardarAnalisis(new Analisis("a2", "Dos", "c2", "fp2", EstadoAnalisis.EnProceso,
            new DateTime(2026, 1, 2), new DateTime(2026, 1, 2)));

        var lista = repo.ListarAnalisis();

        Assert.Equal(2, lista.Count);
        Assert.Equal("a2", lista[0].Id); // más reciente primero
        Assert.Equal("a1", lista[1].Id);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_ruta)) File.Delete(_ruta);
    }
}
