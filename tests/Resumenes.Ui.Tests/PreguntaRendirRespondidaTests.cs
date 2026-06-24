using Resumenes.Core.Modelos;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Tests;

public class PreguntaRendirRespondidaTests
{
    private static PreguntaExamen Preg(TipoPregunta t, string datos)
        => new() { Id = "p", ExamenId = "e", Enunciado = "?", Puntos = 1, Tipo = t, DatosJson = datos };

    [Fact]
    public void Desarrollo_SinTexto_NoRespondida()
    {
        var vm = new PreguntaRendirVm(Preg(TipoPregunta.Desarrollo, "{}"));
        Assert.False(vm.Respondida);
    }

    [Fact]
    public void Desarrollo_ConTexto_Respondida()
    {
        var vm = new PreguntaRendirVm(Preg(TipoPregunta.Desarrollo, "{}"));
        vm.TextoRespuesta = "algo";
        Assert.True(vm.Respondida);
    }
}
