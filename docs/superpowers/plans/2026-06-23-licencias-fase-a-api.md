# Licencias — Fase A: API en Railway — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir y dejar lista para desplegar en Railway la API de licencias: emite/valida claves, registra activaciones por máquina con límite, firma tokens ES256 y permite administrar (crear/revocar/liberar).

**Architecture:** ASP.NET Core Minimal API (.NET 9) + EF Core con provider configurable (Postgres en Railway vía `DATABASE_URL`, SQLite en dev/test). La firma de tokens usa ECDSA P-256 (ES256) con la clave privada en variable de entorno. Sin migraciones EF: el esquema se crea con `EnsureCreated()`.

**Tech Stack:** .NET 9, ASP.NET Core Minimal API, EF Core 9 (Npgsql + Sqlite), `Microsoft.IdentityModel.JsonWebTokens`, xUnit + `Microsoft.AspNetCore.Mvc.Testing`, Docker (deploy).

## Global Constraints

- **TargetFramework:** `net9.0` en todos los proyectos nuevos. `ImplicitUsings=enable`, `Nullable=enable`.
- **Solución:** `Resumenes.sln` en la raíz. Todo proyecto nuevo se agrega con `dotnet sln add`.
- **Identidad git del repo:** commitear con la identidad **local** ya configurada (`emmavzmymtec / emmavzmymtec@gmail.com`). No tocar la global. Verificar con `git config --get user.email` antes del primer commit.
- **Test framework:** xUnit 2.9.2 (igual que `tests/Resumenes.Tests`). `Microsoft.NET.Test.Sdk` 17.12.0, `<Using Include="Xunit" />` global.
- **Secretos por entorno (nunca en el repo):** `FIRMA_PRIVADA_PEM` (PEM de la clave privada EC), `ADMIN_KEY` (secreto admin), `DATABASE_URL` (la inyecta Railway). En dev/test, sin `DATABASE_URL` → SQLite.
- **Algoritmo de firma:** ES256 (ECDSA, curva nistP256). El cliente (Fase B) validará con la clave **pública** correspondiente.
- **Formato de clave de licencia:** `RESU-XXXXX-XXXXX-XXXXX-XXXXX`, alfabeto Crockford base32 sin caracteres ambiguos (`0123456789ABCDEFGHJKMNPQRSTVWXYZ`).
- **Estados de licencia:** `"activa"` | `"revocada"` (strings en minúscula).
- **Códigos de error de activación (en el cuerpo JSON):** `"clave_invalida"` | `"revocada"` | `"limite_alcanzado"`.
- **Máquinas por licencia (default):** `2`.

---

## File Structure

**`src/Resumenes.Licencias.Api/`** (proyecto nuevo, NO se distribuye con la app):
- `Resumenes.Licencias.Api.csproj` — Minimal API + EF Core + JWT.
- `Program.cs` — bootstrap, DI, mapeo de endpoints, `EnsureCreated`, modo `gen-keys`. Termina con `public partial class Program {}`.
- `Datos/Licencia.cs`, `Datos/Activacion.cs` — entidades EF.
- `Datos/LicenciasDbContext.cs` — DbContext + índices únicos.
- `Datos/ConfiguracionBd.cs` — elige provider (Postgres/SQLite) y convierte `DATABASE_URL` a connection string Npgsql.
- `Servicios/GeneradorClaves.cs` — genera/valida formato de clave (puro).
- `Servicios/FirmadorTokens.cs` — firma JWT ES256 desde un PEM privado.
- `Servicios/ServicioActivacion.cs` — reglas de activar/validar (con DB).
- `Contratos/Dtos.cs` — records de request/response y resultados.
- `Endpoints/EndpointsPublicos.cs` — `/activar`, `/validar`.
- `Endpoints/EndpointsAdmin.cs` — `/admin/...` + chequeo de `X-Admin-Key`.
- `appsettings.json` — config no secreta.
- `Dockerfile`, `.dockerignore` — build para Railway.
- `peticiones.http` — smoke tests manuales.
- `README.md` — despliegue en Railway, variables, generación de claves.

**`tests/Resumenes.Licencias.Api.Tests/`** (proyecto nuevo):
- `Resumenes.Licencias.Api.Tests.csproj`.
- `FabricaApiPruebas.cs` — `WebApplicationFactory<Program>` con SQLite in-memory.
- `SaludTests.cs`, `GeneradorClavesTests.cs`, `FirmadorTokensTests.cs`, `ConfiguracionBdTests.cs`, `ServicioActivacionTests.cs`, `EndpointsPublicosTests.cs`, `EndpointsAdminTests.cs`.

---

## Task 1: Esqueleto de la API + health check + fábrica de pruebas

**Files:**
- Create: `src/Resumenes.Licencias.Api/Resumenes.Licencias.Api.csproj`
- Create: `src/Resumenes.Licencias.Api/Program.cs`
- Create: `tests/Resumenes.Licencias.Api.Tests/Resumenes.Licencias.Api.Tests.csproj`
- Create: `tests/Resumenes.Licencias.Api.Tests/SaludTests.cs`

**Interfaces:**
- Produces: endpoint `GET /salud` → `200 "ok"`. `public partial class Program {}` para que los tests usen `WebApplicationFactory<Program>`.

- [ ] **Step 1: Crear el proyecto Web API y el de tests, y sumarlos a la solución**

```bash
cd /d/Desarrollo/Programacion/Resumenes
dotnet new web -n Resumenes.Licencias.Api -o src/Resumenes.Licencias.Api --framework net9.0
dotnet new xunit -n Resumenes.Licencias.Api.Tests -o tests/Resumenes.Licencias.Api.Tests --framework net9.0
dotnet sln Resumenes.sln add src/Resumenes.Licencias.Api/Resumenes.Licencias.Api.csproj
dotnet sln Resumenes.sln add tests/Resumenes.Licencias.Api.Tests/Resumenes.Licencias.Api.Tests.csproj
dotnet add tests/Resumenes.Licencias.Api.Tests reference src/Resumenes.Licencias.Api
dotnet add tests/Resumenes.Licencias.Api.Tests package Microsoft.AspNetCore.Mvc.Testing --version 9.0.0
```

- [ ] **Step 2: Reescribir `Program.cs` con el endpoint de salud**

`src/Resumenes.Licencias.Api/Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/salud", () => Results.Text("ok"));

app.Run();

public partial class Program { }
```

- [ ] **Step 3: Escribir el test que falla**

`tests/Resumenes.Licencias.Api.Tests/SaludTests.cs`:

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Resumenes.Licencias.Api.Tests;

public class SaludTests
{
    [Fact]
    public async Task Salud_DevuelveOk()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var cliente = factory.CreateClient();

        var resp = await cliente.GetAsync("/salud");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("ok", await resp.Content.ReadAsStringAsync());
    }
}
```

- [ ] **Step 4: Correr el test y verificar que pasa**

Run: `dotnet test tests/Resumenes.Licencias.Api.Tests --filter Salud_DevuelveOk`
Expected: PASS (1 passed).

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Licencias.Api tests/Resumenes.Licencias.Api.Tests Resumenes.sln
git commit -m "feat(licencias-api): esqueleto Minimal API + health check + fabrica de pruebas"
```

---

## Task 2: GeneradorClaves (lógica pura)

**Files:**
- Create: `src/Resumenes.Licencias.Api/Servicios/GeneradorClaves.cs`
- Create: `tests/Resumenes.Licencias.Api.Tests/GeneradorClavesTests.cs`

**Interfaces:**
- Produces: `static class GeneradorClaves { string Generar(); bool EsFormatoValido(string clave); }`. Formato `RESU-XXXXX-XXXXX-XXXXX-XXXXX`, alfabeto `0123456789ABCDEFGHJKMNPQRSTVWXYZ`.

- [ ] **Step 1: Escribir los tests que fallan**

`tests/Resumenes.Licencias.Api.Tests/GeneradorClavesTests.cs`:

```csharp
using Resumenes.Licencias.Api.Servicios;

namespace Resumenes.Licencias.Api.Tests;

public class GeneradorClavesTests
{
    [Fact]
    public void Generar_ProduceFormatoEsperado()
    {
        var clave = GeneradorClaves.Generar();

        Assert.StartsWith("RESU-", clave);
        Assert.True(GeneradorClaves.EsFormatoValido(clave), $"clave invalida: {clave}");
        // RESU + 4 grupos de 5 = 4 + 4*(1+5) = 28 chars
        Assert.Equal(28, clave.Length);
    }

    [Fact]
    public void Generar_NoUsaCaracteresAmbiguos()
    {
        for (var i = 0; i < 50; i++)
        {
            var cuerpo = GeneradorClaves.Generar().Replace("RESU-", "").Replace("-", "");
            Assert.DoesNotContain('I', cuerpo);
            Assert.DoesNotContain('L', cuerpo);
            Assert.DoesNotContain('O', cuerpo);
            Assert.DoesNotContain('U', cuerpo);
        }
    }

    [Fact]
    public void Generar_ProduceClavesDistintas()
    {
        var a = GeneradorClaves.Generar();
        var b = GeneradorClaves.Generar();
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData("")]
    [InlineData("RESU-12345")]
    [InlineData("XXXX-ABCDE-ABCDE-ABCDE-ABCDE")]
    [InlineData("RESU-ABCDE-ABCDE-ABCDE-ABCDI")] // I no permitida
    [InlineData("resu-abcde-abcde-abcde-abcde")] // minúsculas
    public void EsFormatoValido_RechazaInvalidas(string clave)
    {
        Assert.False(GeneradorClaves.EsFormatoValido(clave));
    }
}
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test tests/Resumenes.Licencias.Api.Tests --filter GeneradorClavesTests`
Expected: FAIL (no compila: `GeneradorClaves` no existe).

- [ ] **Step 3: Implementar `GeneradorClaves`**

`src/Resumenes.Licencias.Api/Servicios/GeneradorClaves.cs`:

```csharp
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Resumenes.Licencias.Api.Servicios;

public static partial class GeneradorClaves
{
    private const string Alfabeto = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford base32

    public static string Generar()
    {
        var grupos = new string[4];
        for (var g = 0; g < 4; g++)
        {
            var chars = new char[5];
            for (var i = 0; i < 5; i++)
                chars[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
            grupos[g] = new string(chars);
        }
        return "RESU-" + string.Join("-", grupos);
    }

    public static bool EsFormatoValido(string clave)
        => !string.IsNullOrEmpty(clave) && Patron().IsMatch(clave);

    [GeneratedRegex("^RESU-[0-9A-HJKMNP-TV-Z]{5}(-[0-9A-HJKMNP-TV-Z]{5}){3}$")]
    private static partial Regex Patron();
}
```

- [ ] **Step 4: Correr y verificar que pasan**

Run: `dotnet test tests/Resumenes.Licencias.Api.Tests --filter GeneradorClavesTests`
Expected: PASS (todos).

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Licencias.Api/Servicios/GeneradorClaves.cs tests/Resumenes.Licencias.Api.Tests/GeneradorClavesTests.cs
git commit -m "feat(licencias-api): generador y validador de claves de licencia"
```

---

## Task 3: FirmadorTokens (ES256) + modo de generación de claves

**Files:**
- Create: `src/Resumenes.Licencias.Api/Servicios/FirmadorTokens.cs`
- Modify: `src/Resumenes.Licencias.Api/Program.cs` (modo CLI `gen-keys`)
- Create: `tests/Resumenes.Licencias.Api.Tests/FirmadorTokensTests.cs`
- Modify: `src/Resumenes.Licencias.Api/Resumenes.Licencias.Api.csproj` (paquete JWT)

**Interfaces:**
- Produces: `class FirmadorTokens` con ctor `FirmadorTokens(string pemPrivada)` y `string Firmar(string licenciaId, string hwid, string comprador)`. El JWT lleva claims `lic`, `hwid`, `sub`, `iat`; **sin** `exp` (token perpetuo). Algoritmo ES256.

- [ ] **Step 1: Agregar el paquete JWT**

```bash
dotnet add src/Resumenes.Licencias.Api package Microsoft.IdentityModel.JsonWebTokens --version 8.3.0
```

- [ ] **Step 2: Escribir los tests que fallan**

`tests/Resumenes.Licencias.Api.Tests/FirmadorTokensTests.cs`:

```csharp
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Resumenes.Licencias.Api.Servicios;

namespace Resumenes.Licencias.Api.Tests;

public class FirmadorTokensTests
{
    private static (string priv, string pub) ParDeClaves()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (ec.ExportECPrivateKeyPem(), ec.ExportSubjectPublicKeyInfoPem());
    }

    [Fact]
    public async Task Firmar_ProduceTokenVerificableConLaPublica()
    {
        var (priv, pub) = ParDeClaves();
        var firmador = new FirmadorTokens(priv);

        var token = firmador.Firmar("lic-123", "hw-abc", "Juan Perez");

        using var ecPub = ECDsa.Create();
        ecPub.ImportFromPem(pub);
        var handler = new JsonWebTokenHandler();
        var resultado = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            IssuerSigningKey = new ECDsaSecurityKey(ecPub),
            ValidAlgorithms = ["ES256"],
        });

        Assert.True(resultado.IsValid);
        Assert.Equal("lic-123", resultado.Claims["lic"]);
        Assert.Equal("hw-abc", resultado.Claims["hwid"]);
        Assert.Equal("Juan Perez", resultado.Claims["sub"]);
    }

    [Fact]
    public async Task Firmar_FirmaAlteradaNoValida()
    {
        var (priv, _) = ParDeClaves();
        var (_, otraPub) = ParDeClaves(); // pública que NO corresponde
        var token = new FirmadorTokens(priv).Firmar("lic-1", "hw-1", "X");

        using var ecOtra = ECDsa.Create();
        ecOtra.ImportFromPem(otraPub);
        var resultado = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            IssuerSigningKey = new ECDsaSecurityKey(ecOtra),
            ValidAlgorithms = ["ES256"],
        });

        Assert.False(resultado.IsValid);
    }
}
```

- [ ] **Step 3: Correr y verificar que fallan**

Run: `dotnet test tests/Resumenes.Licencias.Api.Tests --filter FirmadorTokensTests`
Expected: FAIL (`FirmadorTokens` no existe).

- [ ] **Step 4: Implementar `FirmadorTokens`**

`src/Resumenes.Licencias.Api/Servicios/FirmadorTokens.cs`:

```csharp
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Resumenes.Licencias.Api.Servicios;

public sealed class FirmadorTokens
{
    private readonly string _pemPrivada;

    public FirmadorTokens(string pemPrivada) => _pemPrivada = pemPrivada;

    public string Firmar(string licenciaId, string hwid, string comprador)
    {
        using var ec = ECDsa.Create();
        ec.ImportFromPem(_pemPrivada);
        var credenciales = new SigningCredentials(
            new ECDsaSecurityKey(ec), SecurityAlgorithms.EcdsaSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                ["lic"] = licenciaId,
                ["hwid"] = hwid,
                ["sub"] = comprador,
            },
            IssuedAt = DateTime.UtcNow,
            SigningCredentials = credenciales,
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
```

- [ ] **Step 5: Correr y verificar que pasan**

Run: `dotnet test tests/Resumenes.Licencias.Api.Tests --filter FirmadorTokensTests`
Expected: PASS.

- [ ] **Step 6: Agregar el modo `gen-keys` a `Program.cs`**

Insertar al comienzo de `Program.cs`, **antes** de `var builder = ...`:

```csharp
using System.Security.Cryptography;

if (args.Length > 0 && args[0] == "gen-keys")
{
    using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    Console.WriteLine("=== FIRMA_PRIVADA_PEM (variable de entorno en Railway, NO commitear) ===");
    Console.WriteLine(ec.ExportECPrivateKeyPem());
    Console.WriteLine("=== Clave PÚBLICA (se embebe en el cliente, Fase B) ===");
    Console.WriteLine(ec.ExportSubjectPublicKeyInfoPem());
    return;
}
```

- [ ] **Step 7: Verificar el modo `gen-keys` a mano**

Run: `dotnet run --project src/Resumenes.Licencias.Api -- gen-keys`
Expected: imprime un bloque PEM privado y uno público y termina. (No commitear las claves.)

- [ ] **Step 8: Commit**

```bash
git add src/Resumenes.Licencias.Api tests/Resumenes.Licencias.Api.Tests/FirmadorTokensTests.cs
git commit -m "feat(licencias-api): firmador de tokens ES256 + modo gen-keys"
```

---

## Task 4: Modelo EF Core (entidades, DbContext, provider configurable)

**Files:**
- Create: `src/Resumenes.Licencias.Api/Datos/Licencia.cs`
- Create: `src/Resumenes.Licencias.Api/Datos/Activacion.cs`
- Create: `src/Resumenes.Licencias.Api/Datos/LicenciasDbContext.cs`
- Create: `src/Resumenes.Licencias.Api/Datos/ConfiguracionBd.cs`
- Modify: `src/Resumenes.Licencias.Api/Resumenes.Licencias.Api.csproj` (paquetes EF)
- Create: `tests/Resumenes.Licencias.Api.Tests/FabricaApiPruebas.cs`
- Create: `tests/Resumenes.Licencias.Api.Tests/ConfiguracionBdTests.cs`

**Interfaces:**
- Produces:
  - `class Licencia { Guid Id; string Clave; string Comprador; string Email; int MaxMaquinas; string Estado; DateTimeOffset CreadaEn; string? Notas; List<Activacion> Activaciones; }`
  - `class Activacion { Guid Id; Guid LicenciaId; Licencia Licencia; string Hwid; string NombreEquipo; DateTimeOffset PrimeraActivacion; DateTimeOffset UltimaValidacion; }`
  - `class LicenciasDbContext(DbContextOptions<LicenciasDbContext>) { DbSet<Licencia> Licencias; DbSet<Activacion> Activaciones; }`
  - `static class ConfiguracionBd { string ConnectionStringDesde(string? databaseUrl); bool EsPostgres(string? databaseUrl); }`
  - `FabricaApiPruebas : WebApplicationFactory<Program>` con SQLite in-memory (para Tasks 6 y 7).

- [ ] **Step 1: Agregar los paquetes EF Core**

```bash
dotnet add src/Resumenes.Licencias.Api package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.0
dotnet add src/Resumenes.Licencias.Api package Npgsql.EntityFrameworkCore.PostgreSQL --version 9.0.4
dotnet add tests/Resumenes.Licencias.Api.Tests package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.0
```

- [ ] **Step 2: Escribir el test de `ConfiguracionBd` que falla**

`tests/Resumenes.Licencias.Api.Tests/ConfiguracionBdTests.cs`:

```csharp
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
```

- [ ] **Step 3: Correr y verificar que falla**

Run: `dotnet test tests/Resumenes.Licencias.Api.Tests --filter ConfiguracionBdTests`
Expected: FAIL (`ConfiguracionBd` no existe).

- [ ] **Step 4: Implementar entidades, DbContext y `ConfiguracionBd`**

`src/Resumenes.Licencias.Api/Datos/Licencia.cs`:

```csharp
namespace Resumenes.Licencias.Api.Datos;

public class Licencia
{
    public Guid Id { get; set; }
    public string Clave { get; set; } = "";
    public string Comprador { get; set; } = "";
    public string Email { get; set; } = "";
    public int MaxMaquinas { get; set; } = 2;
    public string Estado { get; set; } = "activa";
    public DateTimeOffset CreadaEn { get; set; }
    public string? Notas { get; set; }
    public List<Activacion> Activaciones { get; set; } = new();
}
```

`src/Resumenes.Licencias.Api/Datos/Activacion.cs`:

```csharp
namespace Resumenes.Licencias.Api.Datos;

public class Activacion
{
    public Guid Id { get; set; }
    public Guid LicenciaId { get; set; }
    public Licencia Licencia { get; set; } = null!;
    public string Hwid { get; set; } = "";
    public string NombreEquipo { get; set; } = "";
    public DateTimeOffset PrimeraActivacion { get; set; }
    public DateTimeOffset UltimaValidacion { get; set; }
}
```

`src/Resumenes.Licencias.Api/Datos/LicenciasDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace Resumenes.Licencias.Api.Datos;

public class LicenciasDbContext(DbContextOptions<LicenciasDbContext> opciones)
    : DbContext(opciones)
{
    public DbSet<Licencia> Licencias => Set<Licencia>();
    public DbSet<Activacion> Activaciones => Set<Activacion>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Licencia>().HasIndex(l => l.Clave).IsUnique();
        mb.Entity<Activacion>().HasIndex(a => new { a.LicenciaId, a.Hwid }).IsUnique();
        mb.Entity<Licencia>()
            .HasMany(l => l.Activaciones)
            .WithOne(a => a.Licencia)
            .HasForeignKey(a => a.LicenciaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

`src/Resumenes.Licencias.Api/Datos/ConfiguracionBd.cs`:

```csharp
namespace Resumenes.Licencias.Api.Datos;

public static class ConfiguracionBd
{
    public static bool EsPostgres(string? databaseUrl)
        => !string.IsNullOrWhiteSpace(databaseUrl);

    public static string ConnectionStringDesde(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var partes = uri.UserInfo.Split(':', 2);
        var usuario = Uri.UnescapeDataString(partes[0]);
        var clave = partes.Length > 1 ? Uri.UnescapeDataString(partes[1]) : "";
        var baseDatos = uri.AbsolutePath.TrimStart('/');
        var puerto = uri.Port > 0 ? uri.Port : 5432;

        return $"Host={uri.Host};Port={puerto};Username={usuario};" +
               $"Password={clave};Database={baseDatos};SSL Mode=Require;Trust Server Certificate=true";
    }
}
```

- [ ] **Step 5: Correr y verificar que pasan**

Run: `dotnet test tests/Resumenes.Licencias.Api.Tests --filter ConfiguracionBdTests`
Expected: PASS.

- [ ] **Step 6: Registrar el DbContext en `Program.cs` y crear el esquema**

En `Program.cs`, después de `var builder = WebApplication.CreateBuilder(args);` agregar:

```csharp
using Microsoft.EntityFrameworkCore;
using Resumenes.Licencias.Api.Datos;

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
builder.Services.AddDbContext<LicenciasDbContext>(opt =>
{
    if (ConfiguracionBd.EsPostgres(databaseUrl))
        opt.UseNpgsql(ConfiguracionBd.ConnectionStringDesde(databaseUrl!));
    else
        opt.UseSqlite(builder.Configuration.GetConnectionString("Sqlite")
                      ?? "Data Source=licencias.db");
});
```

Y después de `var app = builder.Build();` agregar (crea el esquema si no existe):

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LicenciasDbContext>();
    db.Database.EnsureCreated();
}
```

- [ ] **Step 7: Crear la fábrica de pruebas con SQLite in-memory**

`tests/Resumenes.Licencias.Api.Tests/FabricaApiPruebas.cs`:

```csharp
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
```

- [ ] **Step 8: Deshabilitar la paralelización de tests (las env vars son globales al proceso)**

Como `FabricaApiPruebas` setea `FIRMA_PRIVADA_PEM`/`ADMIN_KEY` en variables de entorno
**globales**, correr clases de test en paralelo podría pisarlas. Crear
`tests/Resumenes.Licencias.Api.Tests/ConfiguracionXunit.cs`:

```csharp
[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

- [ ] **Step 9: Commit**

```bash
git add src/Resumenes.Licencias.Api tests/Resumenes.Licencias.Api.Tests
git commit -m "feat(licencias-api): modelo EF Core, DbContext y provider configurable (Postgres/SQLite)"
```

---

## Task 5: ServicioActivacion — reglas de activar/validar (con DB)

**Files:**
- Create: `src/Resumenes.Licencias.Api/Contratos/Dtos.cs`
- Create: `src/Resumenes.Licencias.Api/Servicios/ServicioActivacion.cs`
- Create: `tests/Resumenes.Licencias.Api.Tests/ServicioActivacionTests.cs`

**Interfaces:**
- Consumes: `LicenciasDbContext`, `FirmadorTokens`, `GeneradorClaves`.
- Produces:
  - `enum CodigoActivacion { Ok, ClaveInvalida, Revocada, LimiteAlcanzado }`
  - `record ResultadoActivacion(CodigoActivacion Codigo, string? Token)`
  - `enum EstadoValidacion { Activa, Revocada }`
  - `class ServicioActivacion(LicenciasDbContext db, FirmadorTokens firmador)` con:
    - `Task<ResultadoActivacion> ActivarAsync(string clave, string hwid, string nombreEquipo)`
    - `Task<EstadoValidacion> ValidarAsync(Guid licenciaId, string hwid)`

- [ ] **Step 1: Escribir los DTOs y los tests que fallan**

`src/Resumenes.Licencias.Api/Contratos/Dtos.cs`:

```csharp
namespace Resumenes.Licencias.Api.Contratos;

public enum CodigoActivacion { Ok, ClaveInvalida, Revocada, LimiteAlcanzado }
public record ResultadoActivacion(CodigoActivacion Codigo, string? Token);

public enum EstadoValidacion { Activa, Revocada }

public record ActivarRequest(string Clave, string Hwid, string NombreEquipo);
public record ActivarResponse(string Token);
public record ValidarRequest(string LicenciaId, string Hwid);
public record ValidarResponse(string Estado);

public record CrearLicenciaRequest(string Comprador, string Email, int? MaxMaquinas);
public record CrearLicenciaResponse(Guid Id, string Clave, string Comprador, int MaxMaquinas);
```

`tests/Resumenes.Licencias.Api.Tests/ServicioActivacionTests.cs`:

```csharp
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
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test tests/Resumenes.Licencias.Api.Tests --filter ServicioActivacionTests`
Expected: FAIL (`ServicioActivacion` no existe).

- [ ] **Step 3: Implementar `ServicioActivacion`**

`src/Resumenes.Licencias.Api/Servicios/ServicioActivacion.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Resumenes.Licencias.Api.Contratos;
using Resumenes.Licencias.Api.Datos;

namespace Resumenes.Licencias.Api.Servicios;

public class ServicioActivacion(LicenciasDbContext db, FirmadorTokens firmador)
{
    public async Task<ResultadoActivacion> ActivarAsync(string clave, string hwid, string nombreEquipo)
    {
        var lic = await db.Licencias
            .Include(l => l.Activaciones)
            .FirstOrDefaultAsync(l => l.Clave == clave);

        if (lic is null)
            return new ResultadoActivacion(CodigoActivacion.ClaveInvalida, null);
        if (lic.Estado != "activa")
            return new ResultadoActivacion(CodigoActivacion.Revocada, null);

        var ahora = DateTimeOffset.UtcNow;
        var existente = lic.Activaciones.FirstOrDefault(a => a.Hwid == hwid);
        if (existente is null)
        {
            if (lic.Activaciones.Count >= lic.MaxMaquinas)
                return new ResultadoActivacion(CodigoActivacion.LimiteAlcanzado, null);

            lic.Activaciones.Add(new Activacion
            {
                Id = Guid.NewGuid(),
                LicenciaId = lic.Id,
                Hwid = hwid,
                NombreEquipo = nombreEquipo,
                PrimeraActivacion = ahora,
                UltimaValidacion = ahora,
            });
        }
        else
        {
            existente.UltimaValidacion = ahora;
            existente.NombreEquipo = nombreEquipo;
        }
        await db.SaveChangesAsync();

        var token = firmador.Firmar(lic.Id.ToString(), hwid, lic.Comprador);
        return new ResultadoActivacion(CodigoActivacion.Ok, token);
    }

    public async Task<EstadoValidacion> ValidarAsync(Guid licenciaId, string hwid)
    {
        var lic = await db.Licencias
            .Include(l => l.Activaciones)
            .FirstOrDefaultAsync(l => l.Id == licenciaId);

        if (lic is null || lic.Estado != "activa")
            return EstadoValidacion.Revocada;

        var act = lic.Activaciones.FirstOrDefault(a => a.Hwid == hwid);
        if (act is null)
            return EstadoValidacion.Revocada;

        act.UltimaValidacion = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return EstadoValidacion.Activa;
    }
}
```

- [ ] **Step 4: Correr y verificar que pasan**

Run: `dotnet test tests/Resumenes.Licencias.Api.Tests --filter ServicioActivacionTests`
Expected: PASS (todos).

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Licencias.Api/Contratos src/Resumenes.Licencias.Api/Servicios/ServicioActivacion.cs tests/Resumenes.Licencias.Api.Tests/ServicioActivacionTests.cs
git commit -m "feat(licencias-api): reglas de activacion y validacion con limite de maquinas"
```

---

## Task 6: Endpoints públicos `/activar` y `/validar`

**Files:**
- Create: `src/Resumenes.Licencias.Api/Endpoints/EndpointsPublicos.cs`
- Modify: `src/Resumenes.Licencias.Api/Program.cs` (DI + mapeo)
- Create: `tests/Resumenes.Licencias.Api.Tests/EndpointsPublicosTests.cs`

**Interfaces:**
- Consumes: `ServicioActivacion`, DTOs (`ActivarRequest/Response`, `ValidarRequest/Response`), `FabricaApiPruebas`.
- Produces: `static class EndpointsPublicos { void Mapear(WebApplication app); }`; rutas `POST /activar`, `POST /validar`.

- [ ] **Step 1: Escribir los tests de integración que fallan**

`tests/Resumenes.Licencias.Api.Tests/EndpointsPublicosTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Resumenes.Licencias.Api.Contratos;
using Resumenes.Licencias.Api.Datos;

namespace Resumenes.Licencias.Api.Tests;

public class EndpointsPublicosTests
{
    private static FabricaApiPruebas CrearFabrica()
    {
        var f = new FabricaApiPruebas();
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        f.EstablecerFirma(ec.ExportECPrivateKeyPem());
        return f;
    }

    private static async Task<Licencia> Sembrar(FabricaApiPruebas f, int max = 2, string estado = "activa")
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LicenciasDbContext>();
        var lic = new Licencia
        {
            Id = Guid.NewGuid(),
            Clave = "RESU-AAAAA-BBBBB-CCCCC-DDDDD",
            Comprador = "Juan", Email = "j@x.com",
            MaxMaquinas = max, Estado = estado, CreadaEn = DateTimeOffset.UtcNow,
        };
        db.Licencias.Add(lic);
        await db.SaveChangesAsync();
        return lic;
    }

    [Fact]
    public async Task Activar_ClaveValida_200ConToken()
    {
        await using var f = CrearFabrica();
        var lic = await Sembrar(f);
        var cliente = f.CreateClient();

        var resp = await cliente.PostAsJsonAsync("/activar",
            new ActivarRequest(lic.Clave, "hw-1", "PC-Oficina"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var cuerpo = await resp.Content.ReadFromJsonAsync<ActivarResponse>();
        Assert.False(string.IsNullOrEmpty(cuerpo!.Token));
    }

    [Fact]
    public async Task Activar_ClaveInexistente_404ClaveInvalida()
    {
        await using var f = CrearFabrica();
        var cliente = f.CreateClient();

        var resp = await cliente.PostAsJsonAsync("/activar",
            new ActivarRequest("RESU-ZZZZZ-ZZZZZ-ZZZZZ-ZZZZZ", "hw-1", "PC"));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Contains("clave_invalida", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Activar_SuperaLimite_409LimiteAlcanzado()
    {
        await using var f = CrearFabrica();
        var lic = await Sembrar(f, max: 1);
        var cliente = f.CreateClient();
        await cliente.PostAsJsonAsync("/activar", new ActivarRequest(lic.Clave, "hw-1", "PC1"));

        var resp = await cliente.PostAsJsonAsync("/activar",
            new ActivarRequest(lic.Clave, "hw-2", "PC2"));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("limite_alcanzado", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Validar_HwidActivo_200Activa()
    {
        await using var f = CrearFabrica();
        var lic = await Sembrar(f);
        var cliente = f.CreateClient();
        await cliente.PostAsJsonAsync("/activar", new ActivarRequest(lic.Clave, "hw-1", "PC"));

        var resp = await cliente.PostAsJsonAsync("/validar",
            new ValidarRequest(lic.Id.ToString(), "hw-1"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var cuerpo = await resp.Content.ReadFromJsonAsync<ValidarResponse>();
        Assert.Equal("activa", cuerpo!.Estado);
    }

    [Fact]
    public async Task Validar_Revocada_403()
    {
        await using var f = CrearFabrica();
        var lic = await Sembrar(f);
        var cliente = f.CreateClient();

        var resp = await cliente.PostAsJsonAsync("/validar",
            new ValidarRequest(lic.Id.ToString(), "hw-desconocido"));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test tests/Resumenes.Licencias.Api.Tests --filter EndpointsPublicosTests`
Expected: FAIL (rutas no existen / 404 genérico).

- [ ] **Step 3: Implementar `EndpointsPublicos`**

`src/Resumenes.Licencias.Api/Endpoints/EndpointsPublicos.cs`:

```csharp
using Resumenes.Licencias.Api.Contratos;
using Resumenes.Licencias.Api.Servicios;

namespace Resumenes.Licencias.Api.Endpoints;

public static class EndpointsPublicos
{
    public static void Mapear(WebApplication app)
    {
        app.MapPost("/activar", async (ActivarRequest req, ServicioActivacion svc) =>
        {
            if (!GeneradorClaves.EsFormatoValido(req.Clave))
                return Results.NotFound(new { error = "clave_invalida" });

            var r = await svc.ActivarAsync(req.Clave, req.Hwid, req.NombreEquipo);
            return r.Codigo switch
            {
                CodigoActivacion.Ok => Results.Ok(new ActivarResponse(r.Token!)),
                CodigoActivacion.ClaveInvalida => Results.NotFound(new { error = "clave_invalida" }),
                CodigoActivacion.Revocada => Results.Json(new { error = "revocada" }, statusCode: 403),
                CodigoActivacion.LimiteAlcanzado => Results.Json(new { error = "limite_alcanzado" }, statusCode: 409),
                _ => Results.StatusCode(500),
            };
        });

        app.MapPost("/validar", async (ValidarRequest req, ServicioActivacion svc) =>
        {
            if (!Guid.TryParse(req.LicenciaId, out var id))
                return Results.Json(new ValidarResponse("revocada"), statusCode: 403);

            var estado = await svc.ValidarAsync(id, req.Hwid);
            return estado == EstadoValidacion.Activa
                ? Results.Ok(new ValidarResponse("activa"))
                : Results.Json(new ValidarResponse("revocada"), statusCode: 403);
        });
    }
}
```

- [ ] **Step 4: Registrar servicios y mapear en `Program.cs`**

En `Program.cs`, antes de `var app = builder.Build();`:

```csharp
using Resumenes.Licencias.Api.Servicios;

builder.Services.AddScoped<ServicioActivacion>();
builder.Services.AddScoped(_ => new FirmadorTokens(
    Environment.GetEnvironmentVariable("FIRMA_PRIVADA_PEM")
    ?? throw new InvalidOperationException("Falta FIRMA_PRIVADA_PEM")));
```

Y después de la línea del `EnsureCreated`, antes de `app.Run();`:

```csharp
using Resumenes.Licencias.Api.Endpoints;

EndpointsPublicos.Mapear(app);
```

- [ ] **Step 5: Correr y verificar que pasan**

Run: `dotnet test tests/Resumenes.Licencias.Api.Tests --filter EndpointsPublicosTests`
Expected: PASS (todos).

- [ ] **Step 6: Commit**

```bash
git add src/Resumenes.Licencias.Api tests/Resumenes.Licencias.Api.Tests/EndpointsPublicosTests.cs
git commit -m "feat(licencias-api): endpoints publicos /activar y /validar"
```

---

## Task 7: Endpoints de administración + auth por `X-Admin-Key`

**Files:**
- Create: `src/Resumenes.Licencias.Api/Endpoints/EndpointsAdmin.cs`
- Modify: `src/Resumenes.Licencias.Api/Program.cs` (mapeo)
- Create: `tests/Resumenes.Licencias.Api.Tests/EndpointsAdminTests.cs`

**Interfaces:**
- Consumes: `LicenciasDbContext`, `GeneradorClaves`, DTOs (`CrearLicenciaRequest/Response`), `FabricaApiPruebas` (expone `AdminKey`).
- Produces: `static class EndpointsAdmin { void Mapear(WebApplication app); }`; rutas `POST /admin/licencias`, `GET /admin/licencias`, `POST /admin/licencias/{id}/revocar`, `DELETE /admin/activaciones/{id}`. Todas exigen header `X-Admin-Key`.

- [ ] **Step 1: Escribir los tests que fallan**

`tests/Resumenes.Licencias.Api.Tests/EndpointsAdminTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Resumenes.Licencias.Api.Contratos;
using Resumenes.Licencias.Api.Datos;

namespace Resumenes.Licencias.Api.Tests;

public class EndpointsAdminTests
{
    private static FabricaApiPruebas CrearFabrica()
    {
        var f = new FabricaApiPruebas();
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        f.EstablecerFirma(ec.ExportECPrivateKeyPem());
        return f;
    }

    private static HttpClient ClienteAdmin(FabricaApiPruebas f)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Add("X-Admin-Key", f.AdminKey);
        return c;
    }

    [Fact]
    public async Task SinAdminKey_401()
    {
        await using var f = CrearFabrica();
        var c = f.CreateClient();

        var resp = await c.PostAsJsonAsync("/admin/licencias",
            new CrearLicenciaRequest("Juan", "j@x.com", null));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task CrearLicencia_DevuelveClaveValida_YPersiste()
    {
        await using var f = CrearFabrica();
        var c = ClienteAdmin(f);

        var resp = await c.PostAsJsonAsync("/admin/licencias",
            new CrearLicenciaRequest("Juan", "j@x.com", null));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var cuerpo = await resp.Content.ReadFromJsonAsync<CrearLicenciaResponse>();
        Assert.StartsWith("RESU-", cuerpo!.Clave);
        Assert.Equal(2, cuerpo.MaxMaquinas); // default

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LicenciasDbContext>();
        Assert.Equal(1, db.Licencias.Count());
    }

    [Fact]
    public async Task Revocar_CambiaEstadoYValidarDevuelve403()
    {
        await using var f = CrearFabrica();
        var c = ClienteAdmin(f);
        var creada = await (await c.PostAsJsonAsync("/admin/licencias",
            new CrearLicenciaRequest("Juan", "j@x.com", 2)))
            .Content.ReadFromJsonAsync<CrearLicenciaResponse>();
        await c.PostAsJsonAsync("/activar", new ActivarRequest(creada!.Clave, "hw-1", "PC"));

        var resp = await c.PostAsync($"/admin/licencias/{creada.Id}/revocar", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var val = await c.PostAsJsonAsync("/validar",
            new ValidarRequest(creada.Id.ToString(), "hw-1"));
        Assert.Equal(HttpStatusCode.Forbidden, val.StatusCode);
    }

    [Fact]
    public async Task LiberarActivacion_PermiteActivarOtraMaquina()
    {
        await using var f = CrearFabrica();
        var c = ClienteAdmin(f);
        var creada = await (await c.PostAsJsonAsync("/admin/licencias",
            new CrearLicenciaRequest("Juan", "j@x.com", 1)))
            .Content.ReadFromJsonAsync<CrearLicenciaResponse>();
        await c.PostAsJsonAsync("/activar", new ActivarRequest(creada!.Clave, "hw-1", "PC1"));

        // ubicar el id de la activación
        Guid actId;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LicenciasDbContext>();
            actId = db.Activaciones.Single().Id;
        }

        var del = await c.DeleteAsync($"/admin/activaciones/{actId}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        var reactivar = await c.PostAsJsonAsync("/activar",
            new ActivarRequest(creada.Clave, "hw-2", "PC2"));
        Assert.Equal(HttpStatusCode.OK, reactivar.StatusCode);
    }
}
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test tests/Resumenes.Licencias.Api.Tests --filter EndpointsAdminTests`
Expected: FAIL (rutas admin no existen).

- [ ] **Step 3: Implementar `EndpointsAdmin`**

`src/Resumenes.Licencias.Api/Endpoints/EndpointsAdmin.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Resumenes.Licencias.Api.Contratos;
using Resumenes.Licencias.Api.Datos;
using Resumenes.Licencias.Api.Servicios;

namespace Resumenes.Licencias.Api.Endpoints;

public static class EndpointsAdmin
{
    public static void Mapear(WebApplication app)
    {
        var grupo = app.MapGroup("/admin");
        grupo.AddEndpointFilter(async (ctx, next) =>
        {
            var enviada = ctx.HttpContext.Request.Headers["X-Admin-Key"].ToString();
            var esperada = Environment.GetEnvironmentVariable("ADMIN_KEY") ?? "";
            if (!ClaveAdminValida(enviada, esperada))
                return Results.Unauthorized();
            return await next(ctx);
        });

        grupo.MapPost("/licencias", async (CrearLicenciaRequest req, LicenciasDbContext db) =>
        {
            var lic = new Licencia
            {
                Id = Guid.NewGuid(),
                Clave = GeneradorClaves.Generar(),
                Comprador = req.Comprador,
                Email = req.Email,
                MaxMaquinas = req.MaxMaquinas is > 0 ? req.MaxMaquinas.Value : 2,
                Estado = "activa",
                CreadaEn = DateTimeOffset.UtcNow,
            };
            db.Licencias.Add(lic);
            await db.SaveChangesAsync();
            return Results.Ok(new CrearLicenciaResponse(lic.Id, lic.Clave, lic.Comprador, lic.MaxMaquinas));
        });

        grupo.MapGet("/licencias", async (LicenciasDbContext db) =>
            Results.Ok(await db.Licencias.Include(l => l.Activaciones).ToListAsync()));

        grupo.MapPost("/licencias/{id:guid}/revocar", async (Guid id, LicenciasDbContext db) =>
        {
            var lic = await db.Licencias.FindAsync(id);
            if (lic is null) return Results.NotFound();
            lic.Estado = "revocada";
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        grupo.MapDelete("/activaciones/{id:guid}", async (Guid id, LicenciasDbContext db) =>
        {
            var act = await db.Activaciones.FindAsync(id);
            if (act is null) return Results.NotFound();
            db.Activaciones.Remove(act);
            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }

    private static bool ClaveAdminValida(string enviada, string esperada)
    {
        if (string.IsNullOrEmpty(esperada) || string.IsNullOrEmpty(enviada))
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(enviada), Encoding.UTF8.GetBytes(esperada));
    }
}
```

- [ ] **Step 4: Mapear en `Program.cs`**

En `Program.cs`, después de `EndpointsPublicos.Mapear(app);`:

```csharp
EndpointsAdmin.Mapear(app);
```

- [ ] **Step 5: Correr y verificar que pasan**

Run: `dotnet test tests/Resumenes.Licencias.Api.Tests --filter EndpointsAdminTests`
Expected: PASS (todos).

- [ ] **Step 6: Commit**

```bash
git add src/Resumenes.Licencias.Api tests/Resumenes.Licencias.Api.Tests/EndpointsAdminTests.cs
git commit -m "feat(licencias-api): endpoints admin (crear/listar/revocar/liberar) con X-Admin-Key"
```

---

## Task 8: Rate limiting + endurecimiento de configuración

**Files:**
- Modify: `src/Resumenes.Licencias.Api/Program.cs`
- Create: `src/Resumenes.Licencias.Api/appsettings.json`
- Create: `tests/Resumenes.Licencias.Api.Tests/SuiteCompletaTests.cs`

**Interfaces:**
- Produces: rate limiter global con política fija; toda la suite verde corriendo junta.

- [ ] **Step 1: Agregar rate limiting global en `Program.cs`**

En `Program.cs`, antes de `var app = builder.Build();`:

```csharp
using System.Threading.RateLimiting;

builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = 429;
    opt.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "global",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});
```

Y después de `var app = builder.Build();` (antes de mapear endpoints):

```csharp
app.UseRateLimiter();
```

- [ ] **Step 2: Crear `appsettings.json` (sin secretos)**

`src/Resumenes.Licencias.Api/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Sqlite": "Data Source=licencias.db"
  }
}
```

- [ ] **Step 3: Escribir un test de humo de la suite completa**

`tests/Resumenes.Licencias.Api.Tests/SuiteCompletaTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Resumenes.Licencias.Api.Contratos;

namespace Resumenes.Licencias.Api.Tests;

public class SuiteCompletaTests
{
    [Fact]
    public async Task FlujoCompleto_CrearActivarValidarRevocar()
    {
        await using var f = new FabricaApiPruebas();
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        f.EstablecerFirma(ec.ExportECPrivateKeyPem());

        var admin = f.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Admin-Key", f.AdminKey);
        var app = f.CreateClient();

        var creada = await (await admin.PostAsJsonAsync("/admin/licencias",
            new CrearLicenciaRequest("Juan", "j@x.com", 2)))
            .Content.ReadFromJsonAsync<CrearLicenciaResponse>();

        var act = await app.PostAsJsonAsync("/activar",
            new ActivarRequest(creada!.Clave, "hw-1", "PC"));
        Assert.Equal(HttpStatusCode.OK, act.StatusCode);

        var val = await app.PostAsJsonAsync("/validar",
            new ValidarRequest(creada.Id.ToString(), "hw-1"));
        Assert.Equal(HttpStatusCode.OK, val.StatusCode);

        await admin.PostAsync($"/admin/licencias/{creada.Id}/revocar", null);

        var val2 = await app.PostAsJsonAsync("/validar",
            new ValidarRequest(creada.Id.ToString(), "hw-1"));
        Assert.Equal(HttpStatusCode.Forbidden, val2.StatusCode);
    }
}
```

- [ ] **Step 4: Correr TODA la suite**

Run: `dotnet test tests/Resumenes.Licencias.Api.Tests`
Expected: PASS (todas las clases de test verdes).

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Licencias.Api tests/Resumenes.Licencias.Api.Tests/SuiteCompletaTests.cs
git commit -m "feat(licencias-api): rate limiting global, appsettings y test de flujo completo"
```

---

## Task 9: Dockerfile + colección `.http` + README de despliegue en Railway

**Files:**
- Create: `src/Resumenes.Licencias.Api/Dockerfile`
- Create: `src/Resumenes.Licencias.Api/.dockerignore`
- Create: `src/Resumenes.Licencias.Api/peticiones.http`
- Create: `src/Resumenes.Licencias.Api/README.md`

**Interfaces:**
- Produces: imagen Docker construible por Railway; documentación de variables y despliegue.

- [ ] **Step 1: Crear el `Dockerfile` (.NET 9)**

`src/Resumenes.Licencias.Api/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Resumenes.Licencias.Api.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
# Railway expone el puerto vía $PORT
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Resumenes.Licencias.Api.dll"]
```

- [ ] **Step 2: Hacer que la app respete `$PORT` de Railway**

En `Program.cs`, justo después de `var builder = WebApplication.CreateBuilder(args);`:

```csharp
var puerto = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(puerto))
    builder.WebHost.UseUrls($"http://0.0.0.0:{puerto}");
```

- [ ] **Step 3: Crear `.dockerignore`**

`src/Resumenes.Licencias.Api/.dockerignore`:

```
bin/
obj/
*.db
*.user
```

- [ ] **Step 4: Crear la colección `peticiones.http`**

`src/Resumenes.Licencias.Api/peticiones.http`:

```http
@base = http://localhost:5000
@adminKey = PONER_ADMIN_KEY

### Salud
GET {{base}}/salud

### Crear licencia (admin)
POST {{base}}/admin/licencias
X-Admin-Key: {{adminKey}}
Content-Type: application/json

{ "comprador": "Juan Perez", "email": "juan@ejemplo.com", "maxMaquinas": 2 }

### Activar (cliente)
POST {{base}}/activar
Content-Type: application/json

{ "clave": "RESU-XXXXX-XXXXX-XXXXX-XXXXX", "hwid": "hw-demo", "nombreEquipo": "PC-Oficina" }

### Validar (cliente)
POST {{base}}/validar
Content-Type: application/json

{ "licenciaId": "GUID-DE-LA-LICENCIA", "hwid": "hw-demo" }

### Listar licencias (admin)
GET {{base}}/admin/licencias
X-Admin-Key: {{adminKey}}

### Revocar (admin)
POST {{base}}/admin/licencias/GUID-DE-LA-LICENCIA/revocar
X-Admin-Key: {{adminKey}}

### Liberar asiento (admin)
DELETE {{base}}/admin/activaciones/GUID-DE-LA-ACTIVACION
X-Admin-Key: {{adminKey}}
```

- [ ] **Step 5: Crear el `README.md` de la API**

`src/Resumenes.Licencias.Api/README.md`:

```markdown
# API de Licencias

Minimal API (.NET 9) para activar/validar/administrar licencias de la app Resúmenes.
Esquema creado con `EnsureCreated()`. Postgres en Railway, SQLite en local.

## Variables de entorno

| Variable | Para qué | Dónde |
|---|---|---|
| `FIRMA_PRIVADA_PEM` | Clave privada EC (P-256) para firmar tokens | Railway (secreto) |
| `ADMIN_KEY` | Secreto de los endpoints `/admin/*` | Railway (secreto) |
| `DATABASE_URL` | Postgres (la inyecta Railway al sumar el plugin) | Railway |
| `PORT` | Puerto de escucha (lo inyecta Railway) | Railway |

Sin `DATABASE_URL` la API usa SQLite (`licencias.db`) — útil para correr local.

## Generar el par de claves (una sola vez)

    dotnet run --project src/Resumenes.Licencias.Api -- gen-keys

Copiá el bloque **privado** a `FIRMA_PRIVADA_PEM` en Railway. Guardá el bloque
**público**: se embebe en el cliente (Fase B). NO commitear ninguna de las dos.

## Despliegue en Railway

1. Nuevo servicio → "Deploy from repo" apuntando a este subdirectorio (o Root
   Directory = `src/Resumenes.Licencias.Api`). Railway detecta el `Dockerfile`.
2. Agregar el plugin **PostgreSQL** → setea `DATABASE_URL` automáticamente.
3. Cargar variables `FIRMA_PRIVADA_PEM` y `ADMIN_KEY`.
4. Deploy. Probar `GET https://<tu-dominio>.railway.app/salud` → `ok`.

## Correr local

    dotnet run --project src/Resumenes.Licencias.Api
    # usa SQLite; setear ADMIN_KEY y FIRMA_PRIVADA_PEM en el entorno primero
```

- [ ] **Step 6: Verificar que la imagen compila (build local del publish)**

Run: `dotnet publish src/Resumenes.Licencias.Api -c Release -o /tmp/pub-licencias`
Expected: BUILD SUCCEEDED (verifica que el proyecto publica sin errores; el `docker build` real lo hace Railway).

- [ ] **Step 7: Commit**

```bash
git add src/Resumenes.Licencias.Api/Dockerfile src/Resumenes.Licencias.Api/.dockerignore src/Resumenes.Licencias.Api/peticiones.http src/Resumenes.Licencias.Api/README.md src/Resumenes.Licencias.Api/Program.cs
git commit -m "chore(licencias-api): Dockerfile, coleccion .http y README de despliegue Railway"
```

---

## Cierre de la Fase A

Al terminar las 9 tareas tenés:
- API con `/activar`, `/validar` y `/admin/*` (crear, listar, revocar, liberar), firma ES256, límite de máquinas, rate limiting, suite de tests verde.
- Lista para desplegar en Railway con Postgres.

**Pasos manuales del usuario (fuera del código, los hacés vos):**
1. `dotnet run ... -- gen-keys` → cargar `FIRMA_PRIVADA_PEM` y `ADMIN_KEY` en Railway; **guardar la clave pública** para la Fase B.
2. Desplegar en Railway + plugin Postgres.
3. Anotar el **dominio** resultante (lo necesita el cliente en la Fase B).

**Siguiente:** plan de la **Fase B–E (cliente)**, que usa la clave pública y el dominio de Railway de los pasos anteriores.
```
