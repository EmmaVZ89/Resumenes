using Resumenes.Core.Modelos;

namespace Resumenes.Ui.ViewModels;

/// <summary>
/// Parámetro de navegación para la pantalla VistaConfirmarTemas.
/// Encapsula el análisis en curso y los temas detectados en Fase 2.
/// NOTA: la propiedad se llama <c>TemasDetectados</c> (no <c>Temas</c>) a propósito, para no
/// colisionar con el binding <c>{Binding Temas}</c> del VM durante el breve instante en que
/// WPF-UI fija este parámetro como DataContext antes de que la vista lo reemplace por su VM.
/// </summary>
public record ParametroTemas(Analisis An, IReadOnlyList<TemaDetectado> TemasDetectados, string PromptResumen = "");

/// <summary>
/// Parámetro de navegación para la pantalla VistaResultados.
/// Encapsula el análisis cuyo resultado se quiere mostrar.
/// </summary>
public record ParametroResultados(Analisis An);
