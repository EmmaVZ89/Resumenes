using Resumenes.Core.Modelos;
using Resumenes.Core.Orquestacion;
using Resumenes.Tests.Fakes;
using Xunit;

namespace Resumenes.Tests;

public class PipelineOrquestadorTests
{
    private static PasoPipeline Paso(string hash, Action onEjecutar, Etapa etapa = Etapa.LimpiezaIA)
        => new(etapa, "arc1", null, "ruta.txt",
            _ => Task.FromResult(hash),
            ctx => { onEjecutar(); return Task.CompletedTask; });

    [Fact]
    public async Task Ejecuta_pasoPendiente_yLoMarcaCompletado()
    {
        var repo = new RepositorioEnMemoria();
        var orq = new PipelineOrquestador(repo, new RelojFijo());
        int veces = 0;
        var r = await orq.EjecutarAsync("an1", new[] { Paso("h1", () => veces++) }, default);

        Assert.Equal(1, veces);
        Assert.Equal(1, r.Ok);
        var u = repo.ObtenerUnidad("an1", "arc1", null, Etapa.LimpiezaIA)!;
        Assert.Equal(EstadoUnidad.Completado, u.Estado);
        Assert.Equal("h1", u.HashEntrada);
    }

    [Fact]
    public async Task NoReprocesa_siCompletadoYHashIgual()
    {
        var repo = new RepositorioEnMemoria();
        var orq = new PipelineOrquestador(repo, new RelojFijo());
        int veces = 0;
        await orq.EjecutarAsync("an1", new[] { Paso("h1", () => veces++) }, default);
        var r2 = await orq.EjecutarAsync("an1", new[] { Paso("h1", () => veces++) }, default);

        Assert.Equal(1, veces);          // no se volvió a ejecutar
        Assert.Equal(1, r2.Salteados);
    }

    [Fact]
    public async Task Reprocesa_siHashCambia()
    {
        var repo = new RepositorioEnMemoria();
        var orq = new PipelineOrquestador(repo, new RelojFijo());
        int veces = 0;
        await orq.EjecutarAsync("an1", new[] { Paso("h1", () => veces++) }, default);
        await orq.EjecutarAsync("an1", new[] { Paso("h2", () => veces++) }, default);

        Assert.Equal(2, veces);          // cambió el hash => reprocesa
    }

    [Fact]
    public async Task NoReprocesa_siFijadoPorUsuario_aunqueCambieHash()
    {
        var repo = new RepositorioEnMemoria();
        var orq = new PipelineOrquestador(repo, new RelojFijo());
        int veces = 0;
        await orq.EjecutarAsync("an1", new[] { Paso("h1", () => veces++) }, default);
        repo.ObtenerUnidad("an1", "arc1", null, Etapa.LimpiezaIA)!.FijadoPorUsuario = true;
        await orq.EjecutarAsync("an1", new[] { Paso("h2", () => veces++) }, default);

        Assert.Equal(1, veces);          // fijado => no reprocesa
    }

    [Fact]
    public async Task PasoQueLanza_marcaError_yNoCrashea_yDetiene()
    {
        var repo = new RepositorioEnMemoria();
        var orq = new PipelineOrquestador(repo, new RelojFijo());
        int veces2 = 0;
        var pasos = new[]
        {
            new PasoPipeline(Etapa.LimpiezaIA, "arc1", null, "r.txt",
                _ => Task.FromResult("h1"),
                ctx => throw new InvalidOperationException("boom")),
            new PasoPipeline(Etapa.ResumenFinal, "arc1", null, "r2.txt",
                _ => Task.FromResult("h2"),
                ctx => { veces2++; return Task.CompletedTask; }),
        };

        var r = await orq.EjecutarAsync("an1", pasos, default);

        Assert.Equal(1, r.Errores);
        Assert.Equal(0, veces2);         // no continúa la cadena tras el error
        var u = repo.ObtenerUnidad("an1", "arc1", null, Etapa.LimpiezaIA)!;
        Assert.Equal(EstadoUnidad.Error, u.Estado);
        Assert.Contains("boom", u.ErrorMsg);
    }
}
