using System.Collections.ObjectModel;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Tests;

public class EmparejarSinDuplicadosTests
{
    [Fact]
    public void OpcionDeOtraFila_NoApareceDisponible()
    {
        var derecha = new ObservableCollection<string> { "A", "B", "C" };
        var sel = new ObservableCollection<int> { -1, -1 };
        var fila0 = new EmparejamientoItemVm(sel, 0) { TextoIzquierda = "x", Derecha = derecha };
        var fila1 = new EmparejamientoItemVm(sel, 1) { TextoIzquierda = "y", Derecha = derecha };
        fila0.Sincronizar(new[] { fila0, fila1 });
        fila1.Sincronizar(new[] { fila0, fila1 });

        fila0.SeleccionIndice = 1; // fila0 elige "B"

        Assert.DoesNotContain("B", fila1.OpcionesDisponibles.Select(o => o.Texto)); // ya usada por fila0
        Assert.Contains("B", fila0.OpcionesDisponibles.Select(o => o.Texto));       // la propia sí
    }
}
