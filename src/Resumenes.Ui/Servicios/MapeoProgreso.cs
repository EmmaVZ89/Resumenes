using Resumenes.Core.Orquestacion;

namespace Resumenes.Ui.Servicios;

public record EstadoProgreso(string Texto, bool Indeterminado, double FraccionItem, FaseAnalisis Fase, EstadoEvento Estado);

public static class MapeoProgreso
{
    public static EstadoProgreso AEstado(ProgresoPaso e)
    {
        bool indet = e.SubIndice is null || e.SubTotal is null || e.SubTotal == 0;
        double frac = indet ? 0 : (double)e.SubIndice!.Value / e.SubTotal!.Value;
        var item = string.IsNullOrEmpty(e.Item) ? "" : $"{e.Item} — ";
        var texto = $"{item}{(string.IsNullOrEmpty(e.Detalle) ? e.Etapa.ToString() : e.Detalle)}";
        return new EstadoProgreso(texto, indet, frac, e.Fase, e.Estado);
    }
}
