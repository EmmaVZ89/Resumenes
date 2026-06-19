using CommunityToolkit.Mvvm.ComponentModel;

namespace Resumenes.Ui.ViewModels;

/// <summary>
/// Representa un ítem (archivo o tema) dentro del progreso de ejecución.
/// Se muestra en el ItemsControl de VistaEjecutando.
/// </summary>
public partial class ItemProgresoVm : ObservableObject
{
    /// <summary>Nombre del archivo o tema procesado.</summary>
    [ObservableProperty]
    private string _nombre = string.Empty;

    /// <summary>Estado actual del ítem (chip de texto: "Iniciado", "Completado", etc.).</summary>
    [ObservableProperty]
    private string _estado = string.Empty;

    /// <summary>Fracción de avance del ítem (0.0 – 1.0) para la mini-barra de progreso.</summary>
    [ObservableProperty]
    private double _fraccion;
}
