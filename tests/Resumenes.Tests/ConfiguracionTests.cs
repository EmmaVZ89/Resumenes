using Resumenes.Infrastructure.Aplicacion;
using Xunit;

namespace Resumenes.Tests;

public class ConfiguracionTests
{
    [Fact]
    public void Defaults_incluyen_ManifestUrl_y_RutaRuntime()
    {
        var c = new Configuracion();
        Assert.False(string.IsNullOrWhiteSpace(c.ManifestUrl)); // hay una URL por defecto editable
        Assert.NotNull(c.RutaRuntime);                          // "" por defecto (App calcula la real)
    }
}
