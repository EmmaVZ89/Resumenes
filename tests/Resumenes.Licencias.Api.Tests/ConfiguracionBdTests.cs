using Resumenes.Licencias.Api.Datos;

namespace Resumenes.Licencias.Api.Tests;

public class ConfiguracionBdTests
{
    [Fact]
    public void SinDatabaseUrl_NoEsPostgres()
    {
        Assert.False(ConfiguracionBd.EsPostgres(null));
        Assert.False(ConfiguracionBd.EsPostgres(""));
    }

    [Fact]
    public void ConDatabaseUrl_EsPostgres()
    {
        Assert.True(ConfiguracionBd.EsPostgres("postgresql://u:p@host:5432/db"));
    }

    [Fact]
    public void ConvierteUrlPostgresAConnectionStringNpgsql()
    {
        var cs = ConfiguracionBd.ConnectionStringDesde(
            "postgresql://usuario:secreto@maquina.railway.app:5432/railway");

        Assert.Contains("Host=maquina.railway.app", cs);
        Assert.Contains("Port=5432", cs);
        Assert.Contains("Username=usuario", cs);
        Assert.Contains("Password=secreto", cs);
        Assert.Contains("Database=railway", cs);
        Assert.Contains("SSL Mode=Require", cs);
    }
}
