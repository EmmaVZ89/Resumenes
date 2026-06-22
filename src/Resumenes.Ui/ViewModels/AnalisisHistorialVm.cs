using Resumenes.Core.Modelos;

namespace Resumenes.Ui.ViewModels;

/// <summary>
/// Envuelve un <see cref="Analisis"/> para mostrar su tarjeta en el historial.
/// </summary>
public sealed class AnalisisHistorialVm
{
    private readonly Analisis _analisis;
    private readonly string _costoLegible;

    public AnalisisHistorialVm(Analisis analisis, string costoLegible = "")
    {
        _analisis = analisis;
        _costoLegible = costoLegible;
    }

    public string CostoLegible => _costoLegible;
    public bool TieneCosto => !string.IsNullOrEmpty(_costoLegible);

    /// <summary>Id del análisis subyacente.</summary>
    public string Id => _analisis.Id;

    /// <summary>Nombre legible del análisis.</summary>
    public string Nombre => _analisis.Nombre;

    /// <summary>Fecha de última actualización, formateada en español.</summary>
    public string FechaLegible => _analisis.ActualizadoEn.ToString("dd/MM/yyyy HH:mm");

    /// <summary>Estado como cadena legible.</summary>
    public string Estado => _analisis.Estado switch
    {
        EstadoAnalisis.EnProceso   => "En proceso",
        EstadoAnalisis.Completado  => "Completado",
        EstadoAnalisis.ConErrores  => "Con errores",
        EstadoAnalisis.Obsoleto    => "Obsoleto",
        _                          => _analisis.Estado.ToString()
    };

    /// <summary>Ruta de la carpeta de origen.</summary>
    public string CarpetaOrigen => _analisis.CarpetaOrigen;

    /// <summary>True si el análisis está completado.</summary>
    public bool EstaCompleto => _analisis.Estado == EstadoAnalisis.Completado;

    /// <summary>True si el análisis puede continuar (en proceso o con errores).</summary>
    public bool PuedeContinuar => _analisis.Estado is EstadoAnalisis.EnProceso or EstadoAnalisis.ConErrores;
}
