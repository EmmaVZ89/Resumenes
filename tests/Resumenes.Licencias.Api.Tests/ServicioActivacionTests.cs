using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Resumenes.Licencias.Api.Contratos;
using Resumenes.Licencias.Api.Datos;
using Resumenes.Licencias.Api.Servicios;

namespace Resumenes.Licencias.Api.Tests;

public class ServicioActivacionTests : IDisposable
{
    private readonly SqliteConnection _con;
    private readonly LicenciasDbContext _db;
    private readonly ServicioActivacion _sut;

    public ServicioActivacionTests()
    {
        _con = new SqliteConnection("DataSource=:memory:");
        _con.Open();
        _db = new LicenciasDbContext(new DbContextOptionsBuilder<LicenciasDbContext>()
            .UseSqlite(_con).Options);
        _db.Database.EnsureCreated();

        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        _sut = new ServicioActivacion(_db, new FirmadorTokens(ec.ExportECPrivateKeyPem()));
    }

    private async Task<Licencia> SembrarLicencia(int max = 2, string estado = "activa")
    {
        var lic = new Licencia
        {
            Id = Guid.NewGuid(),
            Clave = GeneradorClaves.Generar(),
            Comprador = "Juan",
            Email = "j@x.com",
            MaxMaquinas = max,
            Estado = estado,
            CreadaEn = DateTimeOffset.UtcNow,
        };
        _db.Licencias.Add(lic);
        await _db.SaveChangesAsync();
        return lic;
    }

    [Fact]
    public async Task Activar_ClaveValida_DevuelveTokenYRegistraActivacion()
    {
        var lic = await SembrarLicencia();

        var r = await _sut.ActivarAsync(lic.Clave, "hw-1", "PC-Oficina");

        Assert.Equal(CodigoActivacion.Ok, r.Codigo);
        Assert.False(string.IsNullOrEmpty(r.Token));
        Assert.Equal(1, await _db.Activaciones.CountAsync());
    }

    [Fact]
    public async Task Activar_ClaveInexistente_DevuelveClaveInvalida()
    {
        var r = await _sut.ActivarAsync("RESU-AAAAA-AAAAA-AAAAA-AAAAA", "hw-1", "PC");
        Assert.Equal(CodigoActivacion.ClaveInvalida, r.Codigo);
        Assert.Null(r.Token);
    }

    [Fact]
    public async Task Activar_LicenciaRevocada_DevuelveRevocada()
    {
        var lic = await SembrarLicencia(estado: "revocada");
        var r = await _sut.ActivarAsync(lic.Clave, "hw-1", "PC");
        Assert.Equal(CodigoActivacion.Revocada, r.Codigo);
    }

    [Fact]
    public async Task Activar_MismoHwid_NoConsumeAsientoNuevo()
    {
        var lic = await SembrarLicencia(max: 1);
        await _sut.ActivarAsync(lic.Clave, "hw-1", "PC");

        var r = await _sut.ActivarAsync(lic.Clave, "hw-1", "PC");

        Assert.Equal(CodigoActivacion.Ok, r.Codigo);
        Assert.Equal(1, await _db.Activaciones.CountAsync());
    }

    [Fact]
    public async Task Activar_SuperaLimite_DevuelveLimiteAlcanzado()
    {
        var lic = await SembrarLicencia(max: 2);
        await _sut.ActivarAsync(lic.Clave, "hw-1", "PC1");
        await _sut.ActivarAsync(lic.Clave, "hw-2", "PC2");

        var r = await _sut.ActivarAsync(lic.Clave, "hw-3", "PC3");

        Assert.Equal(CodigoActivacion.LimiteAlcanzado, r.Codigo);
        Assert.Equal(2, await _db.Activaciones.CountAsync());
    }

    [Fact]
    public async Task Validar_Activa_DevuelveActiva()
    {
        var lic = await SembrarLicencia();
        await _sut.ActivarAsync(lic.Clave, "hw-1", "PC");

        Assert.Equal(EstadoValidacion.Activa, await _sut.ValidarAsync(lic.Id, "hw-1"));
    }

    [Fact]
    public async Task Validar_HwidNoRegistrado_DevuelveRevocada()
    {
        var lic = await SembrarLicencia();
        Assert.Equal(EstadoValidacion.Revocada, await _sut.ValidarAsync(lic.Id, "hw-x"));
    }

    [Fact]
    public async Task Validar_LicenciaRevocada_DevuelveRevocada()
    {
        var lic = await SembrarLicencia();
        await _sut.ActivarAsync(lic.Clave, "hw-1", "PC");
        lic.Estado = "revocada";
        await _db.SaveChangesAsync();

        Assert.Equal(EstadoValidacion.Revocada, await _sut.ValidarAsync(lic.Id, "hw-1"));
    }

    public void Dispose() { _db.Dispose(); _con.Dispose(); }
}
