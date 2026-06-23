using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Resumenes.Licencias.Api.Datos;

namespace Resumenes.Licencias.Api.Tests;

public class FabricaApiPruebas : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    public string AdminKey => "test-admin-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_conexion.State != System.Data.ConnectionState.Open)
            _conexion.Open();
        Environment.SetEnvironmentVariable("ADMIN_KEY", AdminKey);
        // PEM de prueba: se setea en EstablecerFirma() antes de crear el cliente.

        builder.ConfigureServices(servicios =>
        {
            var descriptor = servicios.Single(
                d => d.ServiceType == typeof(DbContextOptions<LicenciasDbContext>));
            servicios.Remove(descriptor);
            servicios.AddDbContext<LicenciasDbContext>(opt => opt.UseSqlite(_conexion));
        });
    }

    public void EstablecerFirma(string pemPrivada)
        => Environment.SetEnvironmentVariable("FIRMA_PRIVADA_PEM", pemPrivada);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _conexion.Dispose();
    }
}
