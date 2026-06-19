using CommunityToolkit.Mvvm.ComponentModel;
using Resumenes.Core.Modelos;

namespace Resumenes.Ui.ViewModels;

/// <summary>
/// Envuelve un <see cref="TemaDetectado"/> para permitir la edición in-place de su nombre
/// en la pantalla de confirmación de temas.
/// </summary>
public partial class TemaEditableVm : VistaModeloBase
{
    private readonly TemaDetectado _temaOriginal;

    /// <summary>Nombre editable del tema (se puede modificar vía TextBox).</summary>
    [ObservableProperty]
    private string _nombre;

    /// <summary>Archivos asignados a este tema (solo lectura por ahora).</summary>
    public IReadOnlyList<string> Archivos { get; }

    public TemaEditableVm(TemaDetectado tema)
    {
        _temaOriginal = tema;
        _nombre = tema.Nombre;
        Archivos = tema.Archivos.AsReadOnly();
    }

    /// <summary>
    /// Produce el <see cref="TemaDetectado"/> con el nombre editado y el orden indicado.
    /// Los archivos provienen del original (o de la unión en caso de fusión).
    /// </summary>
    public TemaDetectado ObtenerTemaEditado(int nuevoOrden)
        => _temaOriginal with { Nombre = Nombre.Trim(), Orden = nuevoOrden };

    /// <summary>
    /// Produce un <see cref="TemaDetectado"/> fusionado con otro: une los archivos de ambos.
    /// </summary>
    public TemaDetectado FusionarCon(TemaEditableVm otro, int nuevoOrden)
    {
        var archivosUnidos = _temaOriginal.Archivos
            .Concat(otro._temaOriginal.Archivos)
            .Distinct()
            .ToList();
        return _temaOriginal with
        {
            Nombre = $"{Nombre} / {otro.Nombre}",
            Orden = nuevoOrden,
            Archivos = archivosUnidos
        };
    }
}
