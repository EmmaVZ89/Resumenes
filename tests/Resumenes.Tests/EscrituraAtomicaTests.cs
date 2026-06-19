using System.Text;
using Resumenes.Core.Apoyos;
using Xunit;

namespace Resumenes.Tests;

public class EscrituraAtomicaTests
{
    [Fact]
    public void Escribir_dejaElContenido_yNoDejaTmp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "resumenes_" + Guid.NewGuid().ToString("N"));
        var ruta = Path.Combine(dir, "sub", "salida.txt");
        try
        {
            EscrituraAtomica.Escribir(ruta, "hola ñ áé");
            Assert.Equal("hola ñ áé", File.ReadAllText(ruta, Encoding.UTF8));
            Assert.False(File.Exists(ruta + ".tmp"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Escribir_sobrescribeArchivoExistente()
    {
        var dir = Path.Combine(Path.GetTempPath(), "resumenes_" + Guid.NewGuid().ToString("N"));
        var ruta = Path.Combine(dir, "salida.txt");
        try
        {
            EscrituraAtomica.Escribir(ruta, "v1");
            EscrituraAtomica.Escribir(ruta, "v2");
            Assert.Equal("v2", File.ReadAllText(ruta, Encoding.UTF8));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
