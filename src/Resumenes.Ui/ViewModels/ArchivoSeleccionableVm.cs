using CommunityToolkit.Mvvm.ComponentModel;

namespace Resumenes.Ui.ViewModels;

/// <summary>Archivo candidato a procesar, con checkbox de inclusión.</summary>
public partial class ArchivoSeleccionableVm : ObservableObject
{
    public required string Nombre { get; init; }
    [ObservableProperty] private bool _incluido = true;
}
