using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Tests;

public class RendirMarcarRevisarTests
{
    private sealed class RepoFake : IRepositorioExamenes
    {
        public readonly List<RespuestaUsuario> Guardadas = new();
        public List<RespuestaUsuario> Previas = new();
        public List<PreguntaExamen> Preguntas = new();
        public Examen? ObtenerExamen(string id) => new() { Id = id, AnalisisId = "a", Titulo = "t", ConfigJson = "{}" };
        public IReadOnlyList<PreguntaExamen> ListarPreguntas(string examenId) => Preguntas;
        public IReadOnlyList<RespuestaUsuario> ListarRespuestas(string examenId) => Previas;
        public void GuardarRespuesta(RespuestaUsuario u) { Guardadas.RemoveAll(x => x.Id == u.Id); Guardadas.Add(u); }
        public void GuardarExamen(Examen e) { } public void GuardarPregunta(PreguntaExamen p) { }
        public IReadOnlyList<Examen> ListarExamenes(string analisisId) => new List<Examen>();
        public void EliminarExamen(string id) { }
    }
    private sealed class SvcFake : IServicioExamenes
    {
        public Task<Examen> CrearAsync(string a, string t, ConfigExamen c, CancellationToken ct) => Task.FromResult(new Examen { Id="x", AnalisisId=a, Titulo=t, ConfigJson="{}" });
        public Task<Examen> FinalizarYCorregirAsync(string id, CancellationToken ct) => Task.FromResult(new Examen { Id=id, AnalisisId="a", Titulo="t", ConfigJson="{}" });
        public IReadOnlyList<Examen> Historial(string a) => new List<Examen>();
    }

    private static Analisis An() => new("an1", "n", "c", "fp", EstadoAnalisis.Completado, DateTime.UtcNow, DateTime.UtcNow);

    private static PreguntaExamen Preg(string id, int orden = 1) => new()
    { Id = id, ExamenId = "e", Enunciado = "?", Puntos = 1, Tipo = TipoPregunta.Desarrollo, DatosJson = "{}", Orden = orden };

    [Fact]
    public void GuardarActual_PersisteElMarcado()
    {
        var repo = new RepoFake { Preguntas = { Preg("p1") } };
        var vm = new RendirExamenVm(repo, new SvcFake());
        vm.Cargar("e", An(), 0);
        vm.Actual!.MarcadaParaRevisar = true;
        vm.GuardarActual();
        Assert.True(repo.Guardadas.Single().MarcadaRevisar);
    }

    [Fact]
    public void Cargar_RestauraElMarcadoPrevio()
    {
        var repo = new RepoFake { Preguntas = { Preg("p1") } };
        repo.Previas = new() { new RespuestaUsuario { Id = "e:p1", ExamenId = "e", PreguntaId = "p1", MarcadaRevisar = true } };
        var vm = new RendirExamenVm(repo, new SvcFake());
        vm.Cargar("e", An(), 0);
        Assert.True(vm.Preguntas[0].MarcadaParaRevisar);
    }

    [Fact]
    public void IrAPregunta_CambiaElActual()
    {
        var repo = new RepoFake { Preguntas = { Preg("p1", 1), Preg("p2", 2) } };
        var vm = new RendirExamenVm(repo, new SvcFake());
        vm.Cargar("e", An(), 0);
        vm.IrAPreguntaCommand.Execute(2); // orden 2 (1-based) = segunda pregunta (índice 1)
        Assert.Equal(1, vm.IndiceActual);
        Assert.Same(vm.Preguntas[1], vm.Actual);
    }

    [Fact]
    public void Cargar_MarcaLaPrimeraComoActual()
    {
        var repo = new RepoFake { Preguntas = { Preg("p1"), Preg("p2") } };
        var vm = new RendirExamenVm(repo, new SvcFake());
        vm.Cargar("e", An(), 0);
        Assert.True(vm.Preguntas[0].EsActual);
        Assert.False(vm.Preguntas[1].EsActual);
    }

    [Fact]
    public void IrAPregunta_MueveElEsActual()
    {
        var repo = new RepoFake { Preguntas = { Preg("p1"), Preg("p2") } };
        var vm = new RendirExamenVm(repo, new SvcFake());
        vm.Cargar("e", An(), 0);
        vm.IrAPreguntaCommand.Execute(2);
        Assert.False(vm.Preguntas[0].EsActual);
        Assert.True(vm.Preguntas[1].EsActual);
    }
}
