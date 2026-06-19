using Resumenes.Core.Modelos;
using Resumenes.Core.Orquestacion;
using Resumenes.Ui.Servicios;
using Xunit;

namespace Resumenes.Ui.Tests;

public class MapeoProgresoTests
{
    [Fact]
    public void Avance_con_subindice_es_determinado_y_arma_texto()
    {
        var e = new ProgresoPaso(FaseAnalisis.Limpieza, "a.pdf", 1, 2, Etapa.OcrBruto, "OCR página 2/5", 2, 5, EstadoEvento.Avance);
        var s = MapeoProgreso.AEstado(e);
        Assert.False(s.Indeterminado);
        Assert.Equal(0.4, s.FraccionItem, 3);     // 2/5
        Assert.Contains("OCR", s.Texto);
        Assert.Contains("a.pdf", s.Texto);
    }

    [Fact]
    public void Llamada_IA_sin_subindice_es_indeterminada()
    {
        var e = new ProgresoPaso(FaseAnalisis.Deteccion, "", 0, 0, Etapa.ConsolidacionTemas, "pensando…", null, null, EstadoEvento.Avance);
        var s = MapeoProgreso.AEstado(e);
        Assert.True(s.Indeterminado);
    }
}
