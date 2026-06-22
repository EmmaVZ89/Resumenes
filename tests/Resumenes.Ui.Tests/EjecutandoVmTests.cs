// tests/Resumenes.Ui.Tests/EjecutandoVmTests.cs
using System.Collections.ObjectModel;
using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;
using Resumenes.Core.Orquestacion;
using Resumenes.Ui.ViewModels;
using Xunit;

namespace Resumenes.Ui.Tests;

public class EjecutandoVmTests
{
    private class ServicioFake : IServicioAnalisis
    {
        public Task<Analisis> AbrirOCrearAsync(string c, CancellationToken ct, IReadOnlyCollection<string>? rutasExcluidas = null) =>
            Task.FromResult(new Analisis("an1","n","c","fp",EstadoAnalisis.EnProceso,DateTime.UtcNow,DateTime.UtcNow));
        public IReadOnlyList<string> ListarArchivosCandidatos(string carpeta) => Array.Empty<string>();
        public Task<ResultadoLote> ProcesarArchivosAsync(Analisis an, IProgress<ProgresoPaso>? p, CancellationToken ct)
        {
            p?.Report(new ProgresoPaso(FaseAnalisis.Limpieza,"a.txt",1,1,Etapa.LimpiezaIA,"pensando…",null,null,EstadoEvento.Iniciado));
            p?.Report(new ProgresoPaso(FaseAnalisis.Limpieza,"a.txt",1,1,Etapa.LimpiezaIA,"",null,null,EstadoEvento.Completado));
            return Task.FromResult(new ResultadoLote(1,0,Array.Empty<string>()));
        }
        public Task<IReadOnlyList<TemaDetectado>> DetectarTemasAsync(Analisis an, string pt, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TemaDetectado>>(new[]{ new TemaDetectado("t1","Tema 1",1,new(){"a"}) });
        public Task<ResultadoLote> GenerarPorTemasAsync(Analisis an, IReadOnlyList<TemaDetectado> t, string promptResumen, IProgress<ProgresoPaso>? p, CancellationToken ct)
            => Task.FromResult(new ResultadoLote(t.Count,0,Array.Empty<string>()));
    }

    [Fact]
    public async Task Ejecuta_fase1_y_deteccion_y_expone_temas()
    {
        var an = new Analisis("an1","n","c","fp",EstadoAnalisis.EnProceso,DateTime.UtcNow,DateTime.UtcNow);
        var vm = new EjecutandoVm(new ServicioFake());
        await vm.EjecutarAsync(an, "");
        Assert.NotEmpty(vm.Log);
        Assert.Single(vm.TemasDetectados);
        Assert.Equal("Tema 1", vm.TemasDetectados[0].Nombre);
    }
}
