using System.Collections.ObjectModel;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Tests;

public class EmparejarSinDuplicadosTests
{
    private static (EmparejamientoItemVm f0, EmparejamientoItemVm f1) DosFilas()
    {
        var derecha = new ObservableCollection<string> { "A", "B", "C" };
        var sel = new ObservableCollection<int> { -1, -1 };
        var fila0 = new EmparejamientoItemVm(sel, 0) { TextoIzquierda = "x", Derecha = derecha };
        var fila1 = new EmparejamientoItemVm(sel, 1) { TextoIzquierda = "y", Derecha = derecha };
        fila0.Sincronizar(new[] { fila0, fila1 });
        fila1.Sincronizar(new[] { fila0, fila1 });
        return (fila0, fila1);
    }

    [Fact]
    public void OpcionElegidaPorOtraFila_QuedaDeshabilitada_PeroPresente()
    {
        var (fila0, fila1) = DosFilas();

        fila0.SeleccionIndice = 1; // fila0 elige "B"

        // La colección NO se vacía: las 3 opciones siguen presentes en ambas filas.
        Assert.Equal(3, fila1.OpcionesDisponibles.Count);
        // En fila1, "B" queda deshabilitada (ya la usa fila0).
        Assert.False(fila1.OpcionesDisponibles.Single(o => o.Texto == "B").Habilitada);
        // En fila0 (la propia), "B" sigue habilitada.
        Assert.True(fila0.OpcionesDisponibles.Single(o => o.Texto == "B").Habilitada);
    }

    [Fact]
    public void Seleccionar_NoRompeLaPropiaSeleccion()
    {
        var (fila0, _) = DosFilas();
        fila0.SeleccionIndice = 1;
        Assert.Equal(1, fila0.SeleccionIndice); // la selección persiste (no se resetea)
    }

    [Fact]
    public void LiberarOpcion_LaRehabilitaEnLasOtrasFilas()
    {
        var (fila0, fila1) = DosFilas();
        fila0.SeleccionIndice = 1;                 // ocupa "B"
        Assert.False(fila1.OpcionesDisponibles.Single(o => o.Texto == "B").Habilitada);

        fila0.SeleccionIndice = 0;                 // libera "B", ocupa "A"
        Assert.True(fila1.OpcionesDisponibles.Single(o => o.Texto == "B").Habilitada);
        Assert.False(fila1.OpcionesDisponibles.Single(o => o.Texto == "A").Habilitada);
    }
}
