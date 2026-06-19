using Resumenes.Core.Modelos;

namespace Resumenes.Ui.ViewModels;

/// <summary>
/// Parámetro de navegación para la pantalla VistaEjecutando.
/// Encapsula el análisis a ejecutar y el prompt de temas opcional.
/// </summary>
public record ParametroEjecucion(Analisis An, string Prompt);
