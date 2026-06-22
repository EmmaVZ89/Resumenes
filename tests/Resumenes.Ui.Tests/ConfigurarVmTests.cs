using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;
using Resumenes.Core.Orquestacion;
using Resumenes.Ui.ViewModels;
using Xunit;

namespace Resumenes.Ui.Tests;

public class ConfigurarVmTests
{
    private sealed class ServicioFake : IServicioAnalisis
    {
        public IReadOnlyCollection<string>? ExcluidosRecibidos;
        public IReadOnlyList<string> Candidatos = new List<string> { "a.txt", "b.txt" };

        public IReadOnlyList<string> ListarArchivosCandidatos(string carpeta) => Candidatos;
        public Task<Analisis> AbrirOCrearAsync(string carpeta, CancellationToken ct, IReadOnlyCollection<string>? rutasExcluidas = null)
        {
            ExcluidosRecibidos = rutasExcluidas;
            return Task.FromResult(new Analisis("id", "n", carpeta, "fp", EstadoAnalisis.EnProceso, DateTime.UtcNow, DateTime.UtcNow));
        }
        public Task<ResultadoLote> ProcesarArchivosAsync(Analisis an, IProgress<ProgresoPaso>? p, CancellationToken ct) => Task.FromResult(new ResultadoLote(0, 0, Array.Empty<string>()));
        public Task<IReadOnlyList<TemaDetectado>> DetectarTemasAsync(Analisis an, string prompt, CancellationToken ct) => Task.FromResult<IReadOnlyList<TemaDetectado>>(Array.Empty<TemaDetectado>());
        public Task<ResultadoLote> GenerarPorTemasAsync(Analisis an, IReadOnlyList<TemaDetectado> t, string prompt, IProgress<ProgresoPaso>? p, CancellationToken ct) => Task.FromResult(new ResultadoLote(0, 0, Array.Empty<string>()));
    }

    [Fact]
    public void CargarCandidatos_PoblaArchivosTodosIncluidos()
    {
        var svc = new ServicioFake();
        var vm = new ConfigurarVm(svc, null!);
        vm.CargarCandidatosParaTest(@"C:\mat");
        Assert.Equal(2, vm.Archivos.Count);
        Assert.All(vm.Archivos, a => Assert.True(a.Incluido));
    }

    [Fact]
    public async Task Analizar_PasaLosDesmarcadosComoExcluidos()
    {
        var svc = new ServicioFake();
        var vm = new ConfigurarVm(svc, null!);
        vm.CargarCandidatosParaTest(@"C:\mat");
        vm.CarpetaSeleccionada = @"C:\mat";
        vm.Archivos.First(a => a.Nombre == "b.txt").Incluido = false;

        await vm.AnalizarParaTestAsync();

        Assert.Equal(new[] { "b.txt" }, svc.ExcluidosRecibidos);
    }
}
