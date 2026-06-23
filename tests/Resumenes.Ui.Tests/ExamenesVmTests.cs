using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;
using Resumenes.Ui.ViewModels;
using Xunit;

namespace Resumenes.Ui.Tests;

public class ExamenesVmTests
{
    private sealed class ServicioExamenesFake : IServicioExamenes
    {
        public List<Examen> Lista = new();
        public Task<Examen> CrearAsync(string a, string t, ConfigExamen c, CancellationToken ct) => throw new NotImplementedException();
        public Task<Examen> FinalizarYCorregirAsync(string id, CancellationToken ct) => throw new NotImplementedException();
        public IReadOnlyList<Examen> Historial(string analisisId) => Lista;
    }

    private static Analisis An() => new("an1","n","c","fp",EstadoAnalisis.Completado,DateTime.UtcNow,DateTime.UtcNow);

    [Fact]
    public void Cargar_PoblaHistorial()
    {
        var svc = new ServicioExamenesFake();
        svc.Lista.Add(new Examen { Id="e1", AnalisisId="an1", Titulo="Parcial", Estado=EstadoExamen.Corregido,
            Nota=8, Porcentaje=80, Aprobado=true, CreadoEn=DateTime.UtcNow });
        var vm = new ExamenesVm(svc, null!, null!);

        vm.Cargar(An());

        Assert.Single(vm.Examenes);
        Assert.Equal("Parcial", vm.Examenes[0].Titulo);
        Assert.True(vm.Examenes[0].EstaCorregido);
        Assert.Contains("8", vm.Examenes[0].NotaLegible);
    }
}
