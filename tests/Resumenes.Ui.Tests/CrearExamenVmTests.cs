using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;
using Resumenes.Ui.ViewModels;
using Xunit;

namespace Resumenes.Ui.Tests;

public class CrearExamenVmTests
{
    private sealed class SvcFake : IServicioExamenes
    {
        public ConfigExamen? CfgRecibida;
        public Task<Examen> CrearAsync(string a, string t, ConfigExamen c, CancellationToken ct)
        { CfgRecibida = c; return Task.FromResult(new Examen { Id="e1", AnalisisId=a, Titulo=t, CreadoEn=DateTime.UtcNow }); }
        public Task<Examen> FinalizarYCorregirAsync(string id, CancellationToken ct) => throw new NotImplementedException();
        public IReadOnlyList<Examen> Historial(string analisisId) => Array.Empty<Examen>();
    }
    private static Analisis An() => new("an1","n","c","fp",EstadoAnalisis.Completado,DateTime.UtcNow,DateTime.UtcNow);

    [Fact]
    public async Task Crear_ArmaConfigYLlamaServicio()
    {
        var svc = new SvcFake();
        var vm = new CrearExamenVm(svc, null!);
        vm.Cargar(An());
        vm.CantidadMcUna = 3;
        vm.CantidadDesarrollo = 1;
        vm.PuntosTotales = 10;
        vm.TiempoLimiteMin = 20;
        vm.FuenteRapida = true;

        await vm.CrearParaTestAsync();

        Assert.NotNull(svc.CfgRecibida);
        Assert.Equal("rapido", svc.CfgRecibida!.Fuente);
        Assert.Contains(svc.CfgRecibida.Tipos, t => t.Tipo == TipoPregunta.McUna && t.Cantidad == 3);
        Assert.Contains(svc.CfgRecibida.Tipos, t => t.Tipo == TipoPregunta.Desarrollo && t.Cantidad == 1);
        Assert.DoesNotContain(svc.CfgRecibida.Tipos, t => t.Cantidad == 0);  // no incluir tipos en 0
    }
}
