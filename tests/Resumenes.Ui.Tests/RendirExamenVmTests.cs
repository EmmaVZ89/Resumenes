using Resumenes.Core.Modelos;
using Resumenes.Ui.ViewModels;
using Xunit;

namespace Resumenes.Ui.Tests;

public class RendirExamenVmTests
{
    [Fact]
    public void PreguntaRendirVm_McUna_SerializaIndice()
    {
        var p = new PreguntaExamen { Id="p", ExamenId="e", Tipo=TipoPregunta.McUna, Enunciado="?", Puntos=1,
            DatosJson="{\"opciones\":[{\"texto\":\"A\"},{\"texto\":\"B\"}]}" };
        var vm = new PreguntaRendirVm(p);
        vm.Opciones[1].Seleccionada = true;   // elige B (índice 1)
        Assert.Equal("1", vm.ConstruirRespuestaJson());
    }

    [Fact]
    public void PreguntaRendirVm_Completar_SerializaArray()
    {
        var p = new PreguntaExamen { Id="p", ExamenId="e", Tipo=TipoPregunta.Completar, Enunciado="?", Puntos=1,
            DatosJson="{\"texto\":\"a ___ b ___\",\"respuestas\":[\"x\",\"y\"]}" };
        var vm = new PreguntaRendirVm(p);
        Assert.Equal(2, vm.Huecos.Count);
        vm.Huecos[0].Valor = "uno"; vm.Huecos[1].Valor = "dos";
        Assert.Equal("[\"uno\",\"dos\"]", vm.ConstruirRespuestaJson());
    }

    [Fact]
    public void PreguntaRendirVm_Vf_SerializaObjeto()
    {
        var p = new PreguntaExamen { Id="p", ExamenId="e", Tipo=TipoPregunta.VfJustificado, Enunciado="?", Puntos=1,
            DatosJson="{\"afirmacion\":\"el sol es una estrella\"}" };
        var vm = new PreguntaRendirVm(p);
        vm.Vf = true; vm.TextoRespuesta = "porque emite luz propia";
        var json = vm.ConstruirRespuestaJson();
        Assert.Contains("\"vf\":true", json);
        Assert.Contains("porque emite luz propia", json);
    }

    [Fact]
    public void PreguntaRendirVm_Emparejar_SerializaArrayDePares()
    {
        var p = new PreguntaExamen { Id="p", ExamenId="e", Tipo=TipoPregunta.Emparejar, Enunciado="?", Puntos=1,
            DatosJson="{\"izquierda\":[\"A\",\"B\"],\"derecha\":[\"X\",\"Y\"]}" };
        var vm = new PreguntaRendirVm(p);
        // izquierda[0]→derecha[1], izquierda[1]→derecha[0]
        vm.EmparejamientoItems[0].SeleccionIndice = 1;
        vm.EmparejamientoItems[1].SeleccionIndice = 0;
        Assert.Equal("[[0,1],[1,0]]", vm.ConstruirRespuestaJson());
    }

    [Fact]
    public void PreguntaRendirVm_DesarrolloItems_SerializaArrayDeTextos()
    {
        var p = new PreguntaExamen { Id="p", ExamenId="e", Tipo=TipoPregunta.DesarrolloItems, Enunciado="?", Puntos=1,
            DatosJson="{\"items\":[{\"enunciado\":\"a\"},{\"enunciado\":\"b\"}]}" };
        var vm = new PreguntaRendirVm(p);
        vm.Items[0].Texto = "r1";
        vm.Items[1].Texto = "r2";
        Assert.Equal("[\"r1\",\"r2\"]", vm.ConstruirRespuestaJson());
    }
}
