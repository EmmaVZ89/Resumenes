using Resumenes.Core.Modelos;
using Resumenes.Core.Orquestacion;
using Resumenes.Tests.Fakes;
using Xunit;

namespace Resumenes.Tests;

public class ProgresoOrquestadorTests
{
    [Fact]
    public async Task Reporta_iniciado_y_completado_por_paso()
    {
        var repo = new RepositorioEnMemoria();
        var orq = new PipelineOrquestador(repo, new RelojFijo());
        var eventos = new List<ProgresoPaso>();
        var progreso = new Progress<ProgresoPaso>(e => eventos.Add(e));

        var paso = new PasoPipeline(Etapa.OcrBruto, "arc1", null, "r.txt",
            _ => Task.FromResult("h1"),
            ctx => { ctx.Reportar("OCR", 1, 3); return Task.CompletedTask; });

        // Progress<T> entrega de forma asíncrona; capturamos con un TaskCompletionSource simple.
        await orq.EjecutarAsync("an1", new[] { paso }, default,
            FaseAnalisis.Limpieza, "arc1", 1, 1, progreso);
        await Task.Delay(50);

        Assert.Contains(eventos, e => e.Estado == EstadoEvento.Iniciado && e.Etapa == Etapa.OcrBruto);
        Assert.Contains(eventos, e => e.Estado == EstadoEvento.Avance && e.SubIndice == 1 && e.SubTotal == 3);
        Assert.Contains(eventos, e => e.Estado == EstadoEvento.Completado && e.Etapa == Etapa.OcrBruto);
    }
}
