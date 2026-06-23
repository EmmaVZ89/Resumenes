using Resumenes.Ui.Servicios;
using Xunit;

namespace Resumenes.Ui.Tests;

public class ServicioActualizacionesTests
{
    [Theory]
    [InlineData("v1.2.0", 1, 2, 0)]
    [InlineData("1.2.0", 1, 2, 0)]
    [InlineData("v2.0", 2, 0, 0)]
    [InlineData("v1.3.0-beta", 1, 3, 0)]
    public void ParsearVersion_extrae_major_minor_build(string tag, int ma, int mi, int bu)
    {
        var v = ServicioActualizaciones.ParsearVersion(tag);
        Assert.NotNull(v);
        Assert.Equal(ma, v!.Major);
        Assert.Equal(mi, v.Minor < 0 ? 0 : v.Minor);
        Assert.Equal(bu, v.Build < 0 ? 0 : v.Build);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void ParsearVersion_invalida_devuelve_null(string? tag)
        => Assert.Null(ServicioActualizaciones.ParsearVersion(tag));

    [Fact]
    public void Evaluar_version_mas_nueva_devuelve_info()
    {
        var json = """{"tag_name":"v1.2.0","html_url":"https://github.com/x/y/releases/tag/v1.2.0"}""";
        var info = ServicioActualizaciones.Evaluar(json, new Version(1, 1, 0));
        Assert.NotNull(info);
        Assert.Equal("1.2.0", info!.Version);
        Assert.Contains("v1.2.0", info.Url);
    }

    [Fact]
    public void Evaluar_misma_version_devuelve_null()
    {
        // versión actual 1.1.0.0 (como la del assembly) vs release v1.1.0 ⇒ sin aviso
        var json = """{"tag_name":"v1.1.0","html_url":"u"}""";
        Assert.Null(ServicioActualizaciones.Evaluar(json, new Version(1, 1, 0, 0)));
    }

    [Fact]
    public void Evaluar_version_vieja_devuelve_null()
    {
        var json = """{"tag_name":"v1.0.0","html_url":"u"}""";
        Assert.Null(ServicioActualizaciones.Evaluar(json, new Version(1, 1, 0)));
    }

    [Fact]
    public void Evaluar_json_basura_devuelve_null()
        => Assert.Null(ServicioActualizaciones.Evaluar("no es json", new Version(1, 1, 0)));

    [Fact]
    public void Evaluar_sin_campos_devuelve_null()
        => Assert.Null(ServicioActualizaciones.Evaluar("""{"foo":"bar"}""", new Version(1, 1, 0)));
}
