using Resumenes.Core.Modelos;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Tests;

public class ItemResultadoVmTests
{
    [Fact]
    public void Item_ExponeRespuestaUsuario_Correcta_YEstado()
    {
        var p = new PreguntaExamen
        {
            Id = "p", ExamenId = "e", Enunciado = "Capital de Francia", Puntos = 10, Tipo = TipoPregunta.McUna,
            DatosJson = "{\"opciones\":[{\"texto\":\"Roma\",\"correcta\":false},{\"texto\":\"París\",\"correcta\":true}]}"
        };
        var r = new RespuestaUsuario { Id = "r", ExamenId = "e", PreguntaId = "p", RespuestaJson = "1", PuntosObtenidos = 10, Correcta = true };

        var item = new ItemResultadoVm(p, r);

        Assert.Equal("París", item.RespuestaUsuario);
        Assert.Equal("París", item.RespuestaCorrecta);
        Assert.Equal(EstadoRespuesta.Correcta, item.Estado);
    }

    [Fact]
    public void Item_SinResponder_NoLanza_YEstadoIncorrecta()
    {
        var p = new PreguntaExamen { Id = "p", ExamenId = "e", Enunciado = "X", Puntos = 10, Tipo = TipoPregunta.Desarrollo,
            DatosJson = "{\"criterios\":\"c\",\"respuestaEsperada\":\"esperado\"}" };
        var item = new ItemResultadoVm(p, null);
        Assert.Equal("esperado", item.RespuestaCorrecta);
        Assert.Equal(EstadoRespuesta.Incorrecta, item.Estado);
    }
}
