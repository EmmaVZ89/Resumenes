using Resumenes.Core.Interfaces;
using Resumenes.Infrastructure.Aplicacion;
using Resumenes.Ui.Servicios;
using Resumenes.Ui.ViewModels;
using Xunit;

namespace Resumenes.Ui.Tests;

public class OnboardingVmTests
{
    private class SecretosFake : IAlmacenSecretos
    {
        public string? ObtenerApiKey() => "sk-x";
        public void GuardarApiKey(string key) { }
    }

    private class DescargadorFake : IDescargadorDependencias
    {
        public Task<ResultadoDescarga> DescargarFaltantesAsync(IProgress<EstadoDescarga>? p, CancellationToken ct)
        {
            p?.Report(new EstadoDescarga("python", FaseDescarga.Descargando, 50, 100, 1, 2, "50 / 100 MB"));
            p?.Report(new EstadoDescarga("python", FaseDescarga.Completado, 100, 100, 1, 2, "Listo"));
            return Task.FromResult(new ResultadoDescarga(2, 0, 0, System.Array.Empty<string>()));
        }
    }

    [Fact]
    public async Task DescargarDependencias_actualiza_progreso_y_termina()
    {
        var vm = new OnboardingVm(new SecretosFake(), new Configuracion(), new ServicioNavegacion(), new DescargadorFake());
        await vm.DescargarDependenciasCommand.ExecuteAsync(null);

        Assert.False(vm.Descargando);                 // terminó
        Assert.Contains("100", vm.TextoProgreso);     // reflejó el último progreso
        Assert.Equal(1.0, vm.FraccionGlobal, 3);      // 2 de 2 bundles
    }
}
