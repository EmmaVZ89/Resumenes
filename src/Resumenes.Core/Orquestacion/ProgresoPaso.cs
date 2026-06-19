using Resumenes.Core.Modelos;

namespace Resumenes.Core.Orquestacion;

public enum FaseAnalisis { Limpieza, Deteccion, Generacion }
public enum EstadoEvento { Iniciado, Avance, Completado, Salteado, Error }

public record ProgresoPaso(
    FaseAnalisis Fase,
    string Item,
    int ItemIndice,
    int ItemTotal,
    Etapa Etapa,
    string Detalle,
    int? SubIndice,
    int? SubTotal,
    EstadoEvento Estado);

// Se entrega a cada paso para que reporte sub-progreso real (p. ej. OCR página i/N).
public sealed class ContextoPaso(
    IProgress<ProgresoPaso>? progreso, FaseAnalisis fase, string item, int itemIndice, int itemTotal, Etapa etapa)
{
    public CancellationToken Ct { get; init; }
    public void Reportar(string detalle, int? sub = null, int? subTotal = null) =>
        progreso?.Report(new ProgresoPaso(fase, item, itemIndice, itemTotal, etapa, detalle, sub, subTotal, EstadoEvento.Avance));
}
