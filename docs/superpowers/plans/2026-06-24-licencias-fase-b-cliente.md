# Licencias — Fase B–E: Cliente (gate de activación en la app WPF) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bloquear la app WPF "Resúmenes" hasta que se ingrese una clave de licencia válida, validando contra la API ya desplegada y manteniendo la licencia activa offline con revalidación periódica.

**Architecture:** Lógica pura (validar token JWT ES256 con clave pública embebida, decidir el estado de la licencia) en `Resumenes.Core/Licencias`. Adaptadores Windows (ID de equipo por `MachineGuid`, persistencia con DPAPI, HTTP contra la API) y el orquestador en `Resumenes.Ui/Servicios/Licencia`. Una **ventana de activación** separada hace de gate en `App.OnStartup`: si hay licencia válida arranca `MainWindow`, si no, pide activar.

**Tech Stack:** .NET 9 (Core net9.0, Ui net9.0-windows), WPF + WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2, DPAPI (`System.Security.Cryptography.ProtectedData`), `Microsoft.Win32.Registry`, xUnit. Validación JWT con BCL puro (`ECDsa.VerifyData` + `DSASignatureFormat.IeeeP1363FixedFieldConcatenation`) — sin paquetes de JWT en el cliente.

## Global Constraints

- **TFM:** `Resumenes.Core` = `net9.0`; `Resumenes.Ui` = `net9.0-windows`. `Nullable=enable`, `ImplicitUsings=enable`.
- **Identidad git del repo:** identidad **local** `emmavzmymtec / emmavzmymtec@gmail.com`. Verificá `git config --get user.email` antes de commitear. No tocar la global.
- **Rama:** trabajar en una rama de feature (NO en main directo). Trabajar desde `D:\Desarrollo\Programacion\Resumenes`.
- **API base URL:** `https://resumenes-production.up.railway.app` (se inyecta al `ClienteLicenciasHttp`, como ya se hace con `ServicioActualizaciones`).
- **Clave pública EC P-256 (SPKI base64, NO secreta) a embeber en `ValidadorTokenLicencia`:** `MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAElCSdQgdpkcB1ac/4WXtZZpFcOMy4N8MUClrZoB/7Shg8qELTAggKLOUm16OQzJFVjb1L9GnLNv5MH/kX2UxbUg==`
- **Algoritmo del token:** ES256 (ECDSA P-256 + SHA-256). Firma JWT en formato IEEE P1363 (r‖s, 64 bytes). Claims: `lic`, `hwid`, `sub`, `iat`. **Sin `exp`** (perpetuo).
- **Parámetros:** revalidar online cada **14 días**; gracia offline **30 días** sin contacto exitoso; tras 30 días sin validar, bloquear hasta reconectar.
- **Persistencia:** archivo cifrado con DPAPI (ámbito `CurrentUser`) en `%LOCALAPPDATA%\ResumenesApp\licencia.dat`.
- **ID de equipo:** SHA-256 (hex) del `MachineGuid` de `HKLM\SOFTWARE\Microsoft\Cryptography`. No usar MAC.
- **Tests:** xUnit. Core → `tests/Resumenes.Tests`. Ui → `tests/Resumenes.Ui.Tests`.

---

## File Structure

**`src/Resumenes.Core/Licencias/`** (puro, net9.0):
- `ContratosLicencia.cs` — enums/records: `EstadoLicenciaCliente`, `ClaimsLicencia`, `ResultadoValidacionToken`, `DatosLicenciaGuardada`, `ResultadoActivacion`, `EstadoValidacionServidor`.
- `Puertos.cs` — interfaces: `IServicioHwid`, `IAlmacenLicencia`, `IClienteLicencias`.
- `ValidadorTokenLicencia.cs` — valida firma ES256 + hwid con la clave pública embebida.
- `EvaluadorEstadoLicencia.cs` — decide el estado a partir de (token válido, última validación, ahora).

**`src/Resumenes.Ui/Servicios/Licencia/`** (net9.0-windows):
- `ServicioHwidWindows.cs` — `IServicioHwid` vía `MachineGuid`.
- `AlmacenLicenciaDpapi.cs` — `IAlmacenLicencia` vía DPAPI.
- `ClienteLicenciasHttp.cs` — `IClienteLicencias` vía `HttpClient`.
- `ServicioLicencia.cs` — orquestador (activar, estado inicial, revalidar).

**`src/Resumenes.Ui/ViewModels/`**: `ActivacionVm.cs`.
**`src/Resumenes.Ui/Vistas/`**: `VentanaActivacion.xaml` + `.xaml.cs`.
**`src/Resumenes.Ui/App.xaml.cs`**: registro DI + gate en `OnStartup`.
**`src/Resumenes.Ui/Resumenes.Ui.csproj`**: `PackageReference` explícito a `System.Security.Cryptography.ProtectedData` y `Microsoft.Win32.Registry`.

---

## Task 1: Contratos y puertos de licencia (Core)

**Files:**
- Create: `src/Resumenes.Core/Licencias/ContratosLicencia.cs`
- Create: `src/Resumenes.Core/Licencias/Puertos.cs`
- Test: (ninguno propio; son tipos. Se ejercitan en Tasks 2–3.)

**Interfaces:**
- Produces:
  - `enum EstadoLicenciaCliente { SinLicencia, Activa, RevalidarAhora, BloqueadaPorGracia, Revocada }`
  - `record ClaimsLicencia(string LicenciaId, string Hwid, string Comprador, DateTime EmitidoEn)`
  - `record ResultadoValidacionToken(bool Valido, ClaimsLicencia? Claims)`
  - `record DatosLicenciaGuardada(string Token, DateTime UltimaValidacionExitosa)`
  - `enum EstadoValidacionServidor { Activa, Revocada, SinConexion }`
  - `record ResultadoActivacion(bool Exitoso, string? Token, string? Error)`
  - `interface IServicioHwid { string ObtenerIdEquipo(); }`
  - `interface IAlmacenLicencia { DatosLicenciaGuardada? Leer(); void Guardar(DatosLicenciaGuardada datos); void Borrar(); }`
  - `interface IClienteLicencias { Task<ResultadoActivacion> ActivarAsync(string clave, string hwid, string nombreEquipo, CancellationToken ct); Task<EstadoValidacionServidor> ValidarAsync(string licenciaId, string hwid, CancellationToken ct); }`

- [ ] **Step 1: Crear `ContratosLicencia.cs`**

```csharp
namespace Resumenes.Core.Licencias;

public enum EstadoLicenciaCliente { SinLicencia, Activa, RevalidarAhora, BloqueadaPorGracia, Revocada }

public record ClaimsLicencia(string LicenciaId, string Hwid, string Comprador, DateTime EmitidoEn);

public record ResultadoValidacionToken(bool Valido, ClaimsLicencia? Claims)
{
    public static ResultadoValidacionToken Invalido { get; } = new(false, null);
}

public record DatosLicenciaGuardada(string Token, DateTime UltimaValidacionExitosa);

public enum EstadoValidacionServidor { Activa, Revocada, SinConexion }

public record ResultadoActivacion(bool Exitoso, string? Token, string? Error);
```

- [ ] **Step 2: Crear `Puertos.cs`**

```csharp
namespace Resumenes.Core.Licencias;

public interface IServicioHwid
{
    string ObtenerIdEquipo();
}

public interface IAlmacenLicencia
{
    DatosLicenciaGuardada? Leer();
    void Guardar(DatosLicenciaGuardada datos);
    void Borrar();
}

public interface IClienteLicencias
{
    Task<ResultadoActivacion> ActivarAsync(string clave, string hwid, string nombreEquipo, CancellationToken ct);
    Task<EstadoValidacionServidor> ValidarAsync(string licenciaId, string hwid, CancellationToken ct);
}
```

- [ ] **Step 3: Compilar Core**

Run: `dotnet build src/Resumenes.Core`
Expected: BUILD SUCCEEDED.

- [ ] **Step 4: Commit**

```bash
git add src/Resumenes.Core/Licencias
git commit -m "feat(licencias-cliente): contratos y puertos de licencia en Core"
```

---

## Task 2: ValidadorTokenLicencia (Core, firma ES256 + hwid)

**Files:**
- Create: `src/Resumenes.Core/Licencias/ValidadorTokenLicencia.cs`
- Test: `tests/Resumenes.Tests/ValidadorTokenLicenciaTests.cs`

**Interfaces:**
- Consumes: `ClaimsLicencia`, `ResultadoValidacionToken`.
- Produces: `class ValidadorTokenLicencia { ResultadoValidacionToken Validar(string token, string hwidEsperado); }`. Constructor sin parámetros usa la clave pública embebida; un constructor `internal ValidadorTokenLicencia(string pubSpkiBase64)` permite inyectar otra clave en tests.

- [ ] **Step 1: Escribir los tests que fallan**

`tests/Resumenes.Tests/ValidadorTokenLicenciaTests.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Resumenes.Core.Licencias;

namespace Resumenes.Tests;

public class ValidadorTokenLicenciaTests
{
    // Firma un JWT ES256 de prueba con la privada dada (formato P1363, como el servidor).
    private static string FirmarJwt(ECDsa ec, string lic, string hwid, string sub, long iat)
    {
        static string B64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64Url(Encoding.UTF8.GetBytes("{\"alg\":\"ES256\",\"typ\":\"JWT\"}"));
        var payloadJson = JsonSerializer.Serialize(new { lic, hwid, sub, iat });
        var payload = B64Url(Encoding.UTF8.GetBytes(payloadJson));
        var firma = ec.SignData(Encoding.ASCII.GetBytes($"{header}.{payload}"),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{header}.{payload}.{B64Url(firma)}";
    }

    [Fact]
    public void Validar_TokenBienFirmadoYHwidCoincide_EsValido()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pub = Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo());
        var token = FirmarJwt(ec, "lic-1", "hw-abc", "Juan", 1700000000);

        var sut = new ValidadorTokenLicencia(pub);
        var r = sut.Validar(token, "hw-abc");

        Assert.True(r.Valido);
        Assert.Equal("lic-1", r.Claims!.LicenciaId);
        Assert.Equal("hw-abc", r.Claims.Hwid);
        Assert.Equal("Juan", r.Claims.Comprador);
    }

    [Fact]
    public void Validar_HwidDistinto_NoEsValido()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pub = Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo());
        var token = FirmarJwt(ec, "lic-1", "hw-abc", "Juan", 1700000000);

        var r = new ValidadorTokenLicencia(pub).Validar(token, "hw-OTRO");

        Assert.False(r.Valido);
    }

    [Fact]
    public void Validar_FirmadoConOtraClave_NoEsValido()
    {
        using var ecFirma = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var ecOtra = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pubOtra = Convert.ToBase64String(ecOtra.ExportSubjectPublicKeyInfo());
        var token = FirmarJwt(ecFirma, "lic-1", "hw-abc", "Juan", 1700000000);

        var r = new ValidadorTokenLicencia(pubOtra).Validar(token, "hw-abc");

        Assert.False(r.Valido);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-jwt")]
    [InlineData("a.b")]
    [InlineData("a.b.c.d")]
    public void Validar_TokenMalFormado_NoEsValido(string token)
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pub = Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo());
        Assert.False(new ValidadorTokenLicencia(pub).Validar(token, "hw-abc").Valido);
    }
}
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test tests/Resumenes.Tests --filter ValidadorTokenLicenciaTests`
Expected: FAIL (`ValidadorTokenLicencia` no existe).

- [ ] **Step 3: Implementar `ValidadorTokenLicencia`**

`src/Resumenes.Core/Licencias/ValidadorTokenLicencia.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Resumenes.Core.Licencias;

public sealed class ValidadorTokenLicencia
{
    // Clave pública EC P-256 (SPKI base64) que corresponde a la privada del servidor.
    private const string ClavePublicaSpki =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAElCSdQgdpkcB1ac/4WXtZZpFcOMy4N8MUClrZoB/7Shg8qELTAggKLOUm16OQzJFVjb1L9GnLNv5MH/kX2UxbUg==";

    private readonly byte[] _pubDer;

    public ValidadorTokenLicencia() : this(ClavePublicaSpki) { }

    internal ValidadorTokenLicencia(string pubSpkiBase64)
        => _pubDer = Convert.FromBase64String(pubSpkiBase64);

    public ResultadoValidacionToken Validar(string token, string hwidEsperado)
    {
        if (string.IsNullOrWhiteSpace(token)) return ResultadoValidacionToken.Invalido;
        var partes = token.Split('.');
        if (partes.Length != 3) return ResultadoValidacionToken.Invalido;

        try
        {
            var firma = DesdeB64Url(partes[2]);
            using var ec = ECDsa.Create();
            ec.ImportSubjectPublicKeyInfo(_pubDer, out _);
            var ok = ec.VerifyData(
                Encoding.ASCII.GetBytes($"{partes[0]}.{partes[1]}"),
                firma, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            if (!ok) return ResultadoValidacionToken.Invalido;

            using var doc = JsonDocument.Parse(DesdeB64Url(partes[1]));
            var raiz = doc.RootElement;
            var lic = raiz.GetProperty("lic").GetString() ?? "";
            var hwid = raiz.GetProperty("hwid").GetString() ?? "";
            var sub = raiz.TryGetProperty("sub", out var s) ? s.GetString() ?? "" : "";
            var iat = raiz.TryGetProperty("iat", out var i) ? i.GetInt64() : 0;

            if (!string.Equals(hwid, hwidEsperado, StringComparison.Ordinal))
                return ResultadoValidacionToken.Invalido;

            return new ResultadoValidacionToken(true,
                new ClaimsLicencia(lic, hwid, sub, DateTimeOffset.FromUnixTimeSeconds(iat).UtcDateTime));
        }
        catch
        {
            return ResultadoValidacionToken.Invalido;
        }
    }

    private static byte[] DesdeB64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        s += (s.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(s);
    }
}
```

- [ ] **Step 4: Correr y verificar que pasan**

Run: `dotnet test tests/Resumenes.Tests --filter ValidadorTokenLicenciaTests`
Expected: PASS (todos).

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Core/Licencias/ValidadorTokenLicencia.cs tests/Resumenes.Tests/ValidadorTokenLicenciaTests.cs
git commit -m "feat(licencias-cliente): validador de token ES256 + hwid (BCL puro)"
```

---

## Task 3: EvaluadorEstadoLicencia (Core, decisión de estado)

**Files:**
- Create: `src/Resumenes.Core/Licencias/EvaluadorEstadoLicencia.cs`
- Test: `tests/Resumenes.Tests/EvaluadorEstadoLicenciaTests.cs`

**Interfaces:**
- Consumes: `EstadoLicenciaCliente`.
- Produces: `static class EvaluadorEstadoLicencia { EstadoLicenciaCliente Evaluar(bool tieneTokenValido, DateTime? ultimaValidacion, DateTime ahora); }`. Umbrales: revalidar a los 14 días; gracia hasta 30 días.

- [ ] **Step 1: Escribir los tests que fallan**

`tests/Resumenes.Tests/EvaluadorEstadoLicenciaTests.cs`:

```csharp
using Resumenes.Core.Licencias;

namespace Resumenes.Tests;

public class EvaluadorEstadoLicenciaTests
{
    private static readonly DateTime Ahora = new(2026, 6, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SinTokenValido_DevuelveSinLicencia()
        => Assert.Equal(EstadoLicenciaCliente.SinLicencia,
            EvaluadorEstadoLicencia.Evaluar(false, Ahora.AddDays(-1), Ahora));

    [Fact]
    public void ValidadoHace5Dias_DevuelveActiva()
        => Assert.Equal(EstadoLicenciaCliente.Activa,
            EvaluadorEstadoLicencia.Evaluar(true, Ahora.AddDays(-5), Ahora));

    [Fact]
    public void ValidadoHace14DiasExactos_DevuelveActiva()
        => Assert.Equal(EstadoLicenciaCliente.Activa,
            EvaluadorEstadoLicencia.Evaluar(true, Ahora.AddDays(-14), Ahora));

    [Fact]
    public void ValidadoHace15Dias_DevuelveRevalidarAhora()
        => Assert.Equal(EstadoLicenciaCliente.RevalidarAhora,
            EvaluadorEstadoLicencia.Evaluar(true, Ahora.AddDays(-15), Ahora));

    [Fact]
    public void ValidadoHace30DiasExactos_DevuelveRevalidarAhora()
        => Assert.Equal(EstadoLicenciaCliente.RevalidarAhora,
            EvaluadorEstadoLicencia.Evaluar(true, Ahora.AddDays(-30), Ahora));

    [Fact]
    public void ValidadoHace31Dias_DevuelveBloqueadaPorGracia()
        => Assert.Equal(EstadoLicenciaCliente.BloqueadaPorGracia,
            EvaluadorEstadoLicencia.Evaluar(true, Ahora.AddDays(-31), Ahora));

    [Fact]
    public void TokenValidoSinFechaPrevia_DevuelveRevalidarAhora()
        => Assert.Equal(EstadoLicenciaCliente.RevalidarAhora,
            EvaluadorEstadoLicencia.Evaluar(true, null, Ahora));
}
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test tests/Resumenes.Tests --filter EvaluadorEstadoLicenciaTests`
Expected: FAIL (`EvaluadorEstadoLicencia` no existe).

- [ ] **Step 3: Implementar `EvaluadorEstadoLicencia`**

`src/Resumenes.Core/Licencias/EvaluadorEstadoLicencia.cs`:

```csharp
namespace Resumenes.Core.Licencias;

public static class EvaluadorEstadoLicencia
{
    public static readonly TimeSpan IntervaloRevalidacion = TimeSpan.FromDays(14);
    public static readonly TimeSpan Gracia = TimeSpan.FromDays(30);

    public static EstadoLicenciaCliente Evaluar(
        bool tieneTokenValido, DateTime? ultimaValidacion, DateTime ahora)
    {
        if (!tieneTokenValido) return EstadoLicenciaCliente.SinLicencia;
        if (ultimaValidacion is null) return EstadoLicenciaCliente.RevalidarAhora;

        var transcurrido = ahora - ultimaValidacion.Value;
        if (transcurrido > Gracia) return EstadoLicenciaCliente.BloqueadaPorGracia;
        if (transcurrido > IntervaloRevalidacion) return EstadoLicenciaCliente.RevalidarAhora;
        return EstadoLicenciaCliente.Activa;
    }
}
```

- [ ] **Step 4: Correr y verificar que pasan**

Run: `dotnet test tests/Resumenes.Tests --filter EvaluadorEstadoLicenciaTests`
Expected: PASS (todos).

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Core/Licencias/EvaluadorEstadoLicencia.cs tests/Resumenes.Tests/EvaluadorEstadoLicenciaTests.cs
git commit -m "feat(licencias-cliente): evaluador de estado (revalidacion 14d / gracia 30d)"
```

---

## Task 4: ServicioHwidWindows (Ui, ID de equipo)

**Files:**
- Modify: `src/Resumenes.Ui/Resumenes.Ui.csproj` (paquete Registry)
- Create: `src/Resumenes.Ui/Servicios/Licencia/ServicioHwidWindows.cs`
- Test: `tests/Resumenes.Ui.Tests/ServicioHwidWindowsTests.cs`

**Interfaces:**
- Consumes: `IServicioHwid`.
- Produces: `class ServicioHwidWindows : IServicioHwid` — `ObtenerIdEquipo()` devuelve el SHA-256 hex del `MachineGuid`. Constructor interno `ServicioHwidWindows(string semilla)` para test determinista.

- [ ] **Step 1: Agregar el paquete Registry a `Resumenes.Ui.csproj`**

```bash
dotnet add src/Resumenes.Ui package Microsoft.Win32.Registry --version 5.0.0
dotnet add src/Resumenes.Ui package System.Security.Cryptography.ProtectedData --version 10.0.9
```

- [ ] **Step 2: Escribir los tests que fallan**

`tests/Resumenes.Ui.Tests/ServicioHwidWindowsTests.cs`:

```csharp
using Resumenes.Ui.Servicios.Licencia;

namespace Resumenes.Ui.Tests;

public class ServicioHwidWindowsTests
{
    [Fact]
    public void ObtenerIdEquipo_EsHex64_YDeterministaParaLaMismaSemilla()
    {
        var a = new ServicioHwidWindows("semilla-fija").ObtenerIdEquipo();
        var b = new ServicioHwidWindows("semilla-fija").ObtenerIdEquipo();

        Assert.Equal(a, b);
        Assert.Equal(64, a.Length); // SHA-256 en hex
        Assert.Matches("^[0-9a-f]{64}$", a);
    }

    [Fact]
    public void ObtenerIdEquipo_SemillasDistintas_DanIdsDistintos()
    {
        var a = new ServicioHwidWindows("equipo-A").ObtenerIdEquipo();
        var b = new ServicioHwidWindows("equipo-B").ObtenerIdEquipo();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ObtenerIdEquipo_RealDelRegistro_NoVacio()
    {
        // En Windows real lee el MachineGuid; debe devolver un hex de 64.
        var id = new ServicioHwidWindows().ObtenerIdEquipo();
        Assert.Matches("^[0-9a-f]{64}$", id);
    }
}
```

- [ ] **Step 3: Correr y verificar que fallan**

Run: `dotnet test tests/Resumenes.Ui.Tests --filter ServicioHwidWindowsTests`
Expected: FAIL (`ServicioHwidWindows` no existe).

- [ ] **Step 4: Implementar `ServicioHwidWindows`**

`src/Resumenes.Ui/Servicios/Licencia/ServicioHwidWindows.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using Resumenes.Core.Licencias;

namespace Resumenes.Ui.Servicios.Licencia;

public sealed class ServicioHwidWindows : IServicioHwid
{
    private readonly string _semilla;

    public ServicioHwidWindows() => _semilla = LeerMachineGuid();

    // Para tests deterministas sin tocar el registro.
    internal ServicioHwidWindows(string semilla) => _semilla = semilla;

    public string ObtenerIdEquipo()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("ResumenesApp|" + _semilla));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string LeerMachineGuid()
    {
        // HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid: estable, no requiere admin.
        var valor = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null) as string;
        return string.IsNullOrWhiteSpace(valor) ? "maquina-desconocida" : valor;
    }
}
```

- [ ] **Step 5: Correr y verificar que pasan**

Run: `dotnet test tests/Resumenes.Ui.Tests --filter ServicioHwidWindowsTests`
Expected: PASS (todos).

- [ ] **Step 6: Commit**

```bash
git add src/Resumenes.Ui/Servicios/Licencia/ServicioHwidWindows.cs src/Resumenes.Ui/Resumenes.Ui.csproj tests/Resumenes.Ui.Tests/ServicioHwidWindowsTests.cs
git commit -m "feat(licencias-cliente): ServicioHwidWindows (MachineGuid hasheado)"
```

---

## Task 5: AlmacenLicenciaDpapi (Ui, persistencia cifrada)

**Files:**
- Create: `src/Resumenes.Ui/Servicios/Licencia/AlmacenLicenciaDpapi.cs`
- Test: `tests/Resumenes.Ui.Tests/AlmacenLicenciaDpapiTests.cs`

**Interfaces:**
- Consumes: `IAlmacenLicencia`, `DatosLicenciaGuardada`.
- Produces: `class AlmacenLicenciaDpapi : IAlmacenLicencia`. Constructor `AlmacenLicenciaDpapi(string rutaArchivo)`; serializa `DatosLicenciaGuardada` a JSON, cifra con DPAPI CurrentUser, guarda en el archivo. `Leer()` devuelve null si no existe o no descifra. `Borrar()` elimina el archivo.

- [ ] **Step 1: Escribir los tests que fallan**

`tests/Resumenes.Ui.Tests/AlmacenLicenciaDpapiTests.cs`:

```csharp
using Resumenes.Core.Licencias;
using Resumenes.Ui.Servicios.Licencia;

namespace Resumenes.Ui.Tests;

public class AlmacenLicenciaDpapiTests : IDisposable
{
    private readonly string _ruta = Path.Combine(Path.GetTempPath(), $"lic-test-{Guid.NewGuid():N}.dat");

    [Fact]
    public void GuardarYLeer_DevuelveLosMismosDatos()
    {
        var sut = new AlmacenLicenciaDpapi(_ruta);
        var datos = new DatosLicenciaGuardada("token-xyz", new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc));

        sut.Guardar(datos);
        var leido = sut.Leer();

        Assert.NotNull(leido);
        Assert.Equal("token-xyz", leido!.Token);
        Assert.Equal(datos.UltimaValidacionExitosa, leido.UltimaValidacionExitosa);
    }

    [Fact]
    public void Leer_SinArchivo_DevuelveNull()
        => Assert.Null(new AlmacenLicenciaDpapi(_ruta).Leer());

    [Fact]
    public void Borrar_EliminaElArchivo_YLeerDevuelveNull()
    {
        var sut = new AlmacenLicenciaDpapi(_ruta);
        sut.Guardar(new DatosLicenciaGuardada("t", DateTime.UtcNow));
        sut.Borrar();
        Assert.Null(sut.Leer());
    }

    [Fact]
    public void Leer_ArchivoCorrupto_DevuelveNull()
    {
        File.WriteAllText(_ruta, "esto no es DPAPI valido");
        Assert.Null(new AlmacenLicenciaDpapi(_ruta).Leer());
    }

    public void Dispose() { if (File.Exists(_ruta)) File.Delete(_ruta); }
}
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test tests/Resumenes.Ui.Tests --filter AlmacenLicenciaDpapiTests`
Expected: FAIL (`AlmacenLicenciaDpapi` no existe).

- [ ] **Step 3: Implementar `AlmacenLicenciaDpapi`**

`src/Resumenes.Ui/Servicios/Licencia/AlmacenLicenciaDpapi.cs`:

```csharp
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Resumenes.Core.Licencias;

namespace Resumenes.Ui.Servicios.Licencia;

public sealed class AlmacenLicenciaDpapi : IAlmacenLicencia
{
    private readonly string _ruta;

    public AlmacenLicenciaDpapi(string rutaArchivo) => _ruta = rutaArchivo;

    public DatosLicenciaGuardada? Leer()
    {
        try
        {
            if (!File.Exists(_ruta)) return null;
            var cifrado = File.ReadAllBytes(_ruta);
            var plano = ProtectedData.Unprotect(cifrado, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<DatosLicenciaGuardada>(Encoding.UTF8.GetString(plano));
        }
        catch
        {
            return null; // archivo ausente, corrupto o de otro usuario/máquina
        }
    }

    public void Guardar(DatosLicenciaGuardada datos)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_ruta)!);
        var plano = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(datos));
        var cifrado = ProtectedData.Protect(plano, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_ruta, cifrado);
    }

    public void Borrar()
    {
        if (File.Exists(_ruta)) File.Delete(_ruta);
    }
}
```

- [ ] **Step 4: Correr y verificar que pasan**

Run: `dotnet test tests/Resumenes.Ui.Tests --filter AlmacenLicenciaDpapiTests`
Expected: PASS (todos).

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Ui/Servicios/Licencia/AlmacenLicenciaDpapi.cs tests/Resumenes.Ui.Tests/AlmacenLicenciaDpapiTests.cs
git commit -m "feat(licencias-cliente): AlmacenLicenciaDpapi (token cifrado con DPAPI)"
```

---

## Task 6: ClienteLicenciasHttp (Ui, HTTP contra la API)

**Files:**
- Create: `src/Resumenes.Ui/Servicios/Licencia/ClienteLicenciasHttp.cs`
- Test: `tests/Resumenes.Ui.Tests/ClienteLicenciasHttpTests.cs`

**Interfaces:**
- Consumes: `IClienteLicencias`, `ResultadoActivacion`, `EstadoValidacionServidor`.
- Produces: `class ClienteLicenciasHttp : IClienteLicencias`. Constructor `ClienteLicenciasHttp(HttpClient http, string baseUrl)`. `/activar` → 200 con `{token}` ⇒ Exitoso; 404/403/409 con `{error}` ⇒ no exitoso con ese error; excepción de red ⇒ no exitoso, error `"sin_conexion"`. `/validar` → 200 ⇒ Activa; 403 ⇒ Revocada; excepción ⇒ SinConexion.

- [ ] **Step 1: Escribir los tests que fallan (con un HttpMessageHandler fake)**

`tests/Resumenes.Ui.Tests/ClienteLicenciasHttpTests.cs`:

```csharp
using System.Net;
using System.Net.Http;
using Resumenes.Core.Licencias;
using Resumenes.Ui.Servicios.Licencia;

namespace Resumenes.Ui.Tests;

public class ClienteLicenciasHttpTests
{
    private sealed class HandlerFake(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(responder(request));
    }

    private static ClienteLicenciasHttp Cliente(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => new(new HttpClient(new HandlerFake(responder)), "https://api.test");

    [Fact]
    public async Task Activar_200ConToken_Exitoso()
    {
        var sut = Cliente(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("{\"token\":\"eyJ.abc.def\"}") });

        var r = await sut.ActivarAsync("RESU-AAAAA-BBBBB-CCCCC-DDDDD", "hw-1", "PC", default);

        Assert.True(r.Exitoso);
        Assert.Equal("eyJ.abc.def", r.Token);
    }

    [Fact]
    public async Task Activar_409LimiteAlcanzado_NoExitosoConError()
    {
        var sut = Cliente(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        { Content = new StringContent("{\"error\":\"limite_alcanzado\"}") });

        var r = await sut.ActivarAsync("RESU-AAAAA-BBBBB-CCCCC-DDDDD", "hw-1", "PC", default);

        Assert.False(r.Exitoso);
        Assert.Equal("limite_alcanzado", r.Error);
    }

    [Fact]
    public async Task Activar_SinConexion_ErrorSinConexion()
    {
        var sut = Cliente(_ => throw new HttpRequestException("boom"));
        var r = await sut.ActivarAsync("RESU-AAAAA-BBBBB-CCCCC-DDDDD", "hw-1", "PC", default);
        Assert.False(r.Exitoso);
        Assert.Equal("sin_conexion", r.Error);
    }

    [Fact]
    public async Task Validar_200_Activa()
    {
        var sut = Cliente(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("{\"estado\":\"activa\"}") });
        Assert.Equal(EstadoValidacionServidor.Activa, await sut.ValidarAsync("lic", "hw", default));
    }

    [Fact]
    public async Task Validar_403_Revocada()
    {
        var sut = Cliente(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        { Content = new StringContent("{\"estado\":\"revocada\"}") });
        Assert.Equal(EstadoValidacionServidor.Revocada, await sut.ValidarAsync("lic", "hw", default));
    }

    [Fact]
    public async Task Validar_SinConexion_SinConexion()
    {
        var sut = Cliente(_ => throw new HttpRequestException("boom"));
        Assert.Equal(EstadoValidacionServidor.SinConexion, await sut.ValidarAsync("lic", "hw", default));
    }
}
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test tests/Resumenes.Ui.Tests --filter ClienteLicenciasHttpTests`
Expected: FAIL (`ClienteLicenciasHttp` no existe).

- [ ] **Step 3: Implementar `ClienteLicenciasHttp`**

`src/Resumenes.Ui/Servicios/Licencia/ClienteLicenciasHttp.cs`:

```csharp
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Resumenes.Core.Licencias;

namespace Resumenes.Ui.Servicios.Licencia;

public sealed class ClienteLicenciasHttp : IClienteLicencias
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public ClienteLicenciasHttp(HttpClient http, string baseUrl)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<ResultadoActivacion> ActivarAsync(string clave, string hwid, string nombreEquipo, CancellationToken ct)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}/activar",
                new { clave, hwid, nombreEquipo }, ct);
            var cuerpo = await resp.Content.ReadAsStringAsync(ct);
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var token = LeerCampo(cuerpo, "token");
                return new ResultadoActivacion(true, token, null);
            }
            return new ResultadoActivacion(false, null, LeerCampo(cuerpo, "error") ?? "error");
        }
        catch
        {
            return new ResultadoActivacion(false, null, "sin_conexion");
        }
    }

    public async Task<EstadoValidacionServidor> ValidarAsync(string licenciaId, string hwid, CancellationToken ct)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}/validar",
                new { licenciaId, hwid }, ct);
            return resp.StatusCode == HttpStatusCode.OK
                ? EstadoValidacionServidor.Activa
                : EstadoValidacionServidor.Revocada;
        }
        catch
        {
            return EstadoValidacionServidor.SinConexion;
        }
    }

    private static string? LeerCampo(string json, string campo)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(campo, out var v) ? v.GetString() : null;
        }
        catch { return null; }
    }
}
```

- [ ] **Step 4: Correr y verificar que pasan**

Run: `dotnet test tests/Resumenes.Ui.Tests --filter ClienteLicenciasHttpTests`
Expected: PASS (todos).

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Ui/Servicios/Licencia/ClienteLicenciasHttp.cs tests/Resumenes.Ui.Tests/ClienteLicenciasHttpTests.cs
git commit -m "feat(licencias-cliente): ClienteLicenciasHttp (/activar y /validar)"
```

---

## Task 7: ServicioLicencia (Ui, orquestador)

**Files:**
- Create: `src/Resumenes.Ui/Servicios/Licencia/ServicioLicencia.cs`
- Test: `tests/Resumenes.Ui.Tests/ServicioLicenciaTests.cs`

**Interfaces:**
- Consumes: `IServicioHwid`, `IAlmacenLicencia`, `IClienteLicencias`, `ValidadorTokenLicencia`, `EvaluadorEstadoLicencia`, `IRelojUtc` (ya existe en Core.Interfaces).
- Produces: `class ServicioLicencia(IServicioHwid hwid, IAlmacenLicencia almacen, IClienteLicencias cliente, ValidadorTokenLicencia validador, IRelojUtc reloj)` con:
  - `Task<EstadoLicenciaCliente> ObtenerEstadoAsync(CancellationToken ct)` — valida offline, evalúa, revalida online si toca (con manejo de gracia), persiste, devuelve el estado final (`Activa` | `SinLicencia` | `Revocada` | `BloqueadaPorGracia`).
  - `Task<ResultadoActivacion> ActivarAsync(string clave, string nombreEquipo, CancellationToken ct)` — activa contra la API; si OK, valida el token recibido y lo guarda con `UltimaValidacionExitosa = ahora`.
  - `string IdEquipo` — expone el hwid para mostrarlo en la UI.

- [ ] **Step 1: Escribir los tests que fallan**

`tests/Resumenes.Ui.Tests/ServicioLicenciaTests.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Resumenes.Core.Apoyos;
using Resumenes.Core.Interfaces;
using Resumenes.Core.Licencias;
using Resumenes.Ui.Servicios.Licencia;

namespace Resumenes.Ui.Tests;

public class ServicioLicenciaTests
{
    private const string Hwid = "hw-equipo-1";
    private static readonly DateTime Ahora = new(2026, 6, 24, 12, 0, 0, DateTimeKind.Utc);

    private sealed class HwidFake : IServicioHwid { public string ObtenerIdEquipo() => Hwid; }
    private sealed class RelojFake : IRelojUtc { public DateTime Ahora() => ServicioLicenciaTests.Ahora; }

    private sealed class AlmacenMemoria : IAlmacenLicencia
    {
        public DatosLicenciaGuardada? Datos;
        public DatosLicenciaGuardada? Leer() => Datos;
        public void Guardar(DatosLicenciaGuardada d) => Datos = d;
        public void Borrar() => Datos = null;
    }

    private sealed class ClienteFake(ResultadoActivacion? act = null, EstadoValidacionServidor val = EstadoValidacionServidor.Activa)
        : IClienteLicencias
    {
        public EstadoValidacionServidor Val = val;
        public Task<ResultadoActivacion> ActivarAsync(string c, string h, string n, CancellationToken ct)
            => Task.FromResult(act ?? new ResultadoActivacion(false, null, "clave_invalida"));
        public Task<EstadoValidacionServidor> ValidarAsync(string l, string h, CancellationToken ct)
            => Task.FromResult(Val);
    }

    // Genera (pub, token) reales para el hwid dado, con iat = ahora.
    private static (string pub, string token) ParYToken(string hwid)
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pub = Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo());
        static string B64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var h = B64Url(Encoding.UTF8.GetBytes("{\"alg\":\"ES256\",\"typ\":\"JWT\"}"));
        var p = B64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { lic = "lic-1", hwid, sub = "Juan", iat = 1700000000 })));
        var f = ec.SignData(Encoding.ASCII.GetBytes($"{h}.{p}"), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return (pub, $"{h}.{p}.{B64Url(f)}");
    }

    private static ServicioLicencia Crear(string pub, AlmacenMemoria almacen, IClienteLicencias cliente)
        => new(new HwidFake(), almacen, cliente, new ValidadorTokenLicencia(pub), new RelojFake());

    [Fact]
    public async Task ObtenerEstado_SinDatos_DevuelveSinLicencia()
    {
        var (pub, _) = ParYToken(Hwid);
        var estado = await Crear(pub, new AlmacenMemoria(), new ClienteFake()).ObtenerEstadoAsync(default);
        Assert.Equal(EstadoLicenciaCliente.SinLicencia, estado);
    }

    [Fact]
    public async Task ObtenerEstado_TokenValidoReciente_DevuelveActiva_SinLlamarServidor()
    {
        var (pub, token) = ParYToken(Hwid);
        var almacen = new AlmacenMemoria { Datos = new DatosLicenciaGuardada(token, Ahora.AddDays(-3)) };
        var estado = await Crear(pub, almacen, new ClienteFake()).ObtenerEstadoAsync(default);
        Assert.Equal(EstadoLicenciaCliente.Activa, estado);
    }

    [Fact]
    public async Task ObtenerEstado_TocaRevalidar_ServidorActiva_ActualizaFechaYDevuelveActiva()
    {
        var (pub, token) = ParYToken(Hwid);
        var almacen = new AlmacenMemoria { Datos = new DatosLicenciaGuardada(token, Ahora.AddDays(-20)) };
        var estado = await Crear(pub, almacen, new ClienteFake(val: EstadoValidacionServidor.Activa)).ObtenerEstadoAsync(default);
        Assert.Equal(EstadoLicenciaCliente.Activa, estado);
        Assert.Equal(Ahora, almacen.Datos!.UltimaValidacionExitosa);
    }

    [Fact]
    public async Task ObtenerEstado_TocaRevalidar_ServidorRevoca_BorraYDevuelveRevocada()
    {
        var (pub, token) = ParYToken(Hwid);
        var almacen = new AlmacenMemoria { Datos = new DatosLicenciaGuardada(token, Ahora.AddDays(-20)) };
        var estado = await Crear(pub, almacen, new ClienteFake(val: EstadoValidacionServidor.Revocada)).ObtenerEstadoAsync(default);
        Assert.Equal(EstadoLicenciaCliente.Revocada, estado);
        Assert.Null(almacen.Datos);
    }

    [Fact]
    public async Task ObtenerEstado_TocaRevalidar_SinConexionDentroDeGracia_DevuelveActiva()
    {
        var (pub, token) = ParYToken(Hwid);
        var almacen = new AlmacenMemoria { Datos = new DatosLicenciaGuardada(token, Ahora.AddDays(-20)) };
        var estado = await Crear(pub, almacen, new ClienteFake(val: EstadoValidacionServidor.SinConexion)).ObtenerEstadoAsync(default);
        Assert.Equal(EstadoLicenciaCliente.Activa, estado);
    }

    [Fact]
    public async Task ObtenerEstado_GraciaAgotada_DevuelveBloqueadaPorGracia()
    {
        var (pub, token) = ParYToken(Hwid);
        var almacen = new AlmacenMemoria { Datos = new DatosLicenciaGuardada(token, Ahora.AddDays(-31)) };
        var estado = await Crear(pub, almacen, new ClienteFake(val: EstadoValidacionServidor.SinConexion)).ObtenerEstadoAsync(default);
        Assert.Equal(EstadoLicenciaCliente.BloqueadaPorGracia, estado);
    }

    [Fact]
    public async Task ObtenerEstado_TokenDeOtraMaquina_DevuelveSinLicencia()
    {
        var (pub, tokenOtro) = ParYToken("hw-OTRA-maquina");
        var almacen = new AlmacenMemoria { Datos = new DatosLicenciaGuardada(tokenOtro, Ahora.AddDays(-1)) };
        var estado = await Crear(pub, almacen, new ClienteFake()).ObtenerEstadoAsync(default);
        Assert.Equal(EstadoLicenciaCliente.SinLicencia, estado);
    }

    [Fact]
    public async Task Activar_Exitoso_GuardaTokenConFechaAhora()
    {
        var (pub, token) = ParYToken(Hwid);
        var almacen = new AlmacenMemoria();
        var cliente = new ClienteFake(act: new ResultadoActivacion(true, token, null));
        var r = await Crear(pub, almacen, cliente).ActivarAsync("RESU-AAAAA-BBBBB-CCCCC-DDDDD", "PC", default);
        Assert.True(r.Exitoso);
        Assert.NotNull(almacen.Datos);
        Assert.Equal(token, almacen.Datos!.Token);
        Assert.Equal(Ahora, almacen.Datos.UltimaValidacionExitosa);
    }

    [Fact]
    public async Task Activar_ServidorRechaza_NoGuarda()
    {
        var (pub, _) = ParYToken(Hwid);
        var almacen = new AlmacenMemoria();
        var cliente = new ClienteFake(act: new ResultadoActivacion(false, null, "limite_alcanzado"));
        var r = await Crear(pub, almacen, cliente).ActivarAsync("RESU-AAAAA-BBBBB-CCCCC-DDDDD", "PC", default);
        Assert.False(r.Exitoso);
        Assert.Equal("limite_alcanzado", r.Error);
        Assert.Null(almacen.Datos);
    }
}
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test tests/Resumenes.Ui.Tests --filter ServicioLicenciaTests`
Expected: FAIL (`ServicioLicencia` no existe).

- [ ] **Step 3: Implementar `ServicioLicencia`**

`src/Resumenes.Ui/Servicios/Licencia/ServicioLicencia.cs`:

```csharp
using Resumenes.Core.Interfaces;
using Resumenes.Core.Licencias;

namespace Resumenes.Ui.Servicios.Licencia;

public sealed class ServicioLicencia(
    IServicioHwid hwid,
    IAlmacenLicencia almacen,
    IClienteLicencias cliente,
    ValidadorTokenLicencia validador,
    IRelojUtc reloj)
{
    public string IdEquipo => hwid.ObtenerIdEquipo();

    public async Task<EstadoLicenciaCliente> ObtenerEstadoAsync(CancellationToken ct)
    {
        var datos = almacen.Leer();
        var idEquipo = hwid.ObtenerIdEquipo();
        var validacion = datos is null
            ? ResultadoValidacionToken.Invalido
            : validador.Validar(datos.Token, idEquipo);

        var ahora = reloj.Ahora();
        var estado = EvaluadorEstadoLicencia.Evaluar(
            validacion.Valido, datos?.UltimaValidacionExitosa, ahora);

        if (estado != EstadoLicenciaCliente.RevalidarAhora)
            return estado;

        // Toca revalidar contra el servidor.
        var resp = await cliente.ValidarAsync(validacion.Claims!.LicenciaId, idEquipo, ct);
        switch (resp)
        {
            case EstadoValidacionServidor.Activa:
                almacen.Guardar(datos! with { UltimaValidacionExitosa = ahora });
                return EstadoLicenciaCliente.Activa;
            case EstadoValidacionServidor.Revocada:
                almacen.Borrar();
                return EstadoLicenciaCliente.Revocada;
            default: // SinConexion: seguir con la gracia (ya sabemos que <= 30 días, si no sería BloqueadaPorGracia)
                return EstadoLicenciaCliente.Activa;
        }
    }

    public async Task<ResultadoActivacion> ActivarAsync(string clave, string nombreEquipo, CancellationToken ct)
    {
        var idEquipo = hwid.ObtenerIdEquipo();
        var r = await cliente.ActivarAsync(clave, idEquipo, nombreEquipo, ct);
        if (!r.Exitoso || r.Token is null) return r;

        // El token debe validar contra nuestra clave pública y nuestro hwid.
        if (!validador.Validar(r.Token, idEquipo).Valido)
            return new ResultadoActivacion(false, null, "token_invalido");

        almacen.Guardar(new DatosLicenciaGuardada(r.Token, reloj.Ahora()));
        return r;
    }
}
```

- [ ] **Step 4: Correr y verificar que pasan**

Run: `dotnet test tests/Resumenes.Ui.Tests --filter ServicioLicenciaTests`
Expected: PASS (todos).

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Ui/Servicios/Licencia/ServicioLicencia.cs tests/Resumenes.Ui.Tests/ServicioLicenciaTests.cs
git commit -m "feat(licencias-cliente): ServicioLicencia (estado, revalidacion con gracia, activacion)"
```

---

## Task 8: ActivacionVm (Ui, ViewModel de la pantalla de activación)

**Files:**
- Create: `src/Resumenes.Ui/ViewModels/ActivacionVm.cs`
- Test: `tests/Resumenes.Ui.Tests/ActivacionVmTests.cs`

**Interfaces:**
- Consumes: `ServicioLicencia`.
- Produces: `partial class ActivacionVm : ObservableObject`. Propiedades observables: `Clave` (string), `IdEquipo` (string, solo lectura tras ctor), `Activando` (bool), `MensajeError` (string?), `Activada` (bool). Comando `ActivarCommand` (async). Evento `Action? ActivacionExitosa` que la vista usa para abrir la app. Mapea errores del servidor a mensajes legibles.

- [ ] **Step 1: Escribir los tests que fallan**

`tests/Resumenes.Ui.Tests/ActivacionVmTests.cs`:

```csharp
using Resumenes.Core.Apoyos;
using Resumenes.Core.Interfaces;
using Resumenes.Core.Licencias;
using Resumenes.Ui.Servicios.Licencia;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Tests;

public class ActivacionVmTests
{
    private sealed class HwidFake : IServicioHwid { public string ObtenerIdEquipo() => "hw-visible"; }
    private sealed class RelojFake : IRelojUtc { public DateTime Ahora() => DateTime.UnixEpoch; }
    private sealed class AlmacenMem : IAlmacenLicencia
    {
        public DatosLicenciaGuardada? D;
        public DatosLicenciaGuardada? Leer() => D;
        public void Guardar(DatosLicenciaGuardada d) => D = d;
        public void Borrar() => D = null;
    }
    private sealed class ClienteFake(ResultadoActivacion r) : IClienteLicencias
    {
        public Task<ResultadoActivacion> ActivarAsync(string c, string h, string n, CancellationToken ct) => Task.FromResult(r);
        public Task<EstadoValidacionServidor> ValidarAsync(string l, string h, CancellationToken ct) => Task.FromResult(EstadoValidacionServidor.Activa);
    }

    private static ActivacionVm Crear(ResultadoActivacion respActivar)
    {
        // Validador que acepta cualquier token (clave pública dummy no se usa porque el cliente fake
        // devuelve un token que NO valida → para el test de éxito usamos un servicio que sí valida).
        var svc = new ServicioLicencia(new HwidFake(), new AlmacenMem(), new ClienteFake(respActivar),
            new ValidadorTokenLicencia(DummyPub()), new RelojFake());
        return new ActivacionVm(svc);
    }

    // Clave pública dummy (válida en formato) para construir el ValidadorTokenLicencia.
    private static string DummyPub()
    {
        using var ec = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        return Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo());
    }

    [Fact]
    public void IdEquipo_SeExpone()
    {
        var vm = Crear(new ResultadoActivacion(false, null, "clave_invalida"));
        Assert.Equal("hw-visible", vm.IdEquipo);
    }

    [Fact]
    public async Task Activar_ClaveInvalida_MuestraMensaje_NoActivada()
    {
        var vm = Crear(new ResultadoActivacion(false, null, "clave_invalida"));
        vm.Clave = "RESU-AAAAA-BBBBB-CCCCC-DDDDD";
        await vm.ActivarCommand.ExecuteAsync(null);
        Assert.False(vm.Activada);
        Assert.False(vm.Activando);
        Assert.Contains("inválida", vm.MensajeError!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Activar_LimiteAlcanzado_MensajeDeLimite()
    {
        var vm = Crear(new ResultadoActivacion(false, null, "limite_alcanzado"));
        vm.Clave = "RESU-AAAAA-BBBBB-CCCCC-DDDDD";
        await vm.ActivarCommand.ExecuteAsync(null);
        Assert.Contains("máquinas", vm.MensajeError!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Activar_SinConexion_MensajeDeConexion()
    {
        var vm = Crear(new ResultadoActivacion(false, null, "sin_conexion"));
        vm.Clave = "RESU-AAAAA-BBBBB-CCCCC-DDDDD";
        await vm.ActivarCommand.ExecuteAsync(null);
        Assert.Contains("conexión", vm.MensajeError!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Activar_ClaveVacia_NoLlamaServidor_PideClave()
    {
        var vm = Crear(new ResultadoActivacion(true, "x", null));
        vm.Clave = "   ";
        await vm.ActivarCommand.ExecuteAsync(null);
        Assert.False(vm.Activada);
        Assert.NotNull(vm.MensajeError);
    }
}
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test tests/Resumenes.Ui.Tests --filter ActivacionVmTests`
Expected: FAIL (`ActivacionVm` no existe).

- [ ] **Step 3: Implementar `ActivacionVm`**

`src/Resumenes.Ui/ViewModels/ActivacionVm.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Resumenes.Ui.Servicios.Licencia;

namespace Resumenes.Ui.ViewModels;

public partial class ActivacionVm : ObservableObject
{
    private readonly ServicioLicencia _licencia;

    public ActivacionVm(ServicioLicencia licencia)
    {
        _licencia = licencia;
        IdEquipo = licencia.IdEquipo;
    }

    public string IdEquipo { get; }

    [ObservableProperty] private string _clave = "";
    [ObservableProperty] private bool _activando;
    [ObservableProperty] private string? _mensajeError;
    [ObservableProperty] private bool _activada;

    /// <summary>La vista se suscribe para abrir la app principal al activar con éxito.</summary>
    public Action? ActivacionExitosa { get; set; }

    [RelayCommand]
    private async Task ActivarAsync()
    {
        if (string.IsNullOrWhiteSpace(Clave))
        {
            MensajeError = "Ingresá tu clave de activación.";
            return;
        }

        Activando = true;
        MensajeError = null;
        try
        {
            var nombreEquipo = Environment.MachineName;
            var r = await _licencia.ActivarAsync(Clave.Trim(), nombreEquipo, CancellationToken.None);
            if (r.Exitoso)
            {
                Activada = true;
                ActivacionExitosa?.Invoke();
                return;
            }
            MensajeError = MensajePara(r.Error);
        }
        catch
        {
            MensajeError = "No se pudo activar. Probá de nuevo en un momento.";
        }
        finally
        {
            Activando = false;
        }
    }

    private static string MensajePara(string? error) => error switch
    {
        "clave_invalida" => "La clave es inválida. Revisá que la hayas copiado completa.",
        "revocada" => "Esta licencia fue dada de baja. Contactá al proveedor.",
        "limite_alcanzado" => "La clave ya alcanzó su límite de máquinas. Liberá un equipo o pedí otra licencia.",
        "sin_conexion" => "No hay conexión con el servidor. Verificá tu internet e intentá de nuevo.",
        "token_invalido" => "La respuesta del servidor no se pudo verificar. Probá de nuevo.",
        _ => "No se pudo activar. Probá de nuevo en un momento.",
    };
}
```

- [ ] **Step 4: Correr y verificar que pasan**

Run: `dotnet test tests/Resumenes.Ui.Tests --filter ActivacionVmTests`
Expected: PASS (todos).

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Ui/ViewModels/ActivacionVm.cs tests/Resumenes.Ui.Tests/ActivacionVmTests.cs
git commit -m "feat(licencias-cliente): ActivacionVm (pantalla de activacion)"
```

---

## Task 9: VentanaActivacion + gate de arranque + DI

**Files:**
- Create: `src/Resumenes.Ui/Vistas/VentanaActivacion.xaml`
- Create: `src/Resumenes.Ui/Vistas/VentanaActivacion.xaml.cs`
- Modify: `src/Resumenes.Ui/App.xaml.cs` (registro DI + gate en `OnStartup`)
- Test: (manual; el gate se valida corriendo la app — ver Task 10)

**Interfaces:**
- Consumes: `ActivacionVm`, `ServicioLicencia`, `MainWindow`, `EstadoLicenciaCliente`.
- Produces: `class VentanaActivacion : FluentWindow` que hospeda `ActivacionVm`. El gate decide qué ventana mostrar al iniciar.

- [ ] **Step 1: Crear `VentanaActivacion.xaml`**

`src/Resumenes.Ui/Vistas/VentanaActivacion.xaml`:

```xml
<ui:FluentWindow
    x:Class="Resumenes.Ui.Vistas.VentanaActivacion"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
    Title="Resúmenes — Activación" Width="560" Height="520"
    WindowStartupLocation="CenterScreen" ResizeMode="NoResize"
    ExtendsContentIntoTitleBar="True" WindowBackdropType="Mica">
    <Grid Margin="32">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="Activá tu licencia" FontSize="24" FontWeight="Bold"/>
        <TextBlock Grid.Row="1" Margin="0,8,0,16" TextWrapping="Wrap" Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                   Text="Ingresá la clave que recibiste para desbloquear la app en este equipo."/>

        <ui:TextBox Grid.Row="2" PlaceholderText="RESU-XXXXX-XXXXX-XXXXX-XXXXX"
                    Text="{Binding Clave, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                    FontSize="16" Margin="0,0,0,12"/>

        <ui:Button Grid.Row="3" Content="Activar" Appearance="Primary" HorizontalAlignment="Left"
                   Command="{Binding ActivarCommand}"
                   Icon="{ui:SymbolIcon Symbol=Key24}"/>

        <StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Right"
                    Visibility="{Binding Activando, Converter={StaticResource BoolToVis}}">
            <ui:ProgressRing IsIndeterminate="True" Width="22" Height="22"/>
            <TextBlock Text="Activando…" Margin="8,0,0,0" VerticalAlignment="Center"/>
        </StackPanel>

        <ui:InfoBar Grid.Row="4" Margin="0,12,0,0" Severity="Error" IsOpen="{Binding MensajeError, Converter={StaticResource NotNullToBool}}"
                    Title="No se pudo activar" Message="{Binding MensajeError}"/>

        <TextBlock Grid.Row="5" VerticalAlignment="Bottom" TextWrapping="Wrap" FontSize="12"
                   Foreground="{DynamicResource TextFillColorTertiaryBrush}"
                   Text="{Binding IdEquipo, StringFormat='ID de equipo: {0}'}"/>
    </Grid>
</ui:FluentWindow>
```

> Los converters `BoolToVis` y `NotNullToBool` ya se usan en el proyecto (ver `App.xaml`/recursos de otras vistas). Si `NotNullToBool` no existe, agregá uno trivial en los recursos de la ventana o reemplazá el binding del `InfoBar.IsOpen` por un `BoolToVis` sobre una nueva propiedad `HayError => MensajeError is not null`. **Antes de implementar, verificá en `App.xaml` qué converters están declarados y reusá esos nombres.**

- [ ] **Step 2: Crear `VentanaActivacion.xaml.cs`**

`src/Resumenes.Ui/Vistas/VentanaActivacion.xaml.cs`:

```csharp
using Resumenes.Ui.ViewModels;
using Wpf.Ui.Controls;

namespace Resumenes.Ui.Vistas;

public partial class VentanaActivacion : FluentWindow
{
    public VentanaActivacion(ActivacionVm vm)
    {
        InitializeComponent();
        DataContext = vm;
        // Al activar con éxito, abrir la app y cerrar esta ventana.
        vm.ActivacionExitosa = () =>
        {
            var main = App.Servicios.GetRequiredService<MainWindow>();
            Application.Current.MainWindow = main;
            main.Show();
            Close();
        };
    }
}
```

> Agregá los `using` necesarios: `System.Windows`, `Microsoft.Extensions.DependencyInjection`.

- [ ] **Step 3: Registrar en DI y agregar el gate en `App.xaml.cs`**

En `App.xaml.cs`, junto a los demás registros (antes de `Servicios = sc.BuildServiceProvider();`), agregar:

```csharp
// -------- Licencia --------
sc.AddSingleton<Resumenes.Core.Licencias.IServicioHwid, Resumenes.Ui.Servicios.Licencia.ServicioHwidWindows>();
sc.AddSingleton<Resumenes.Core.Licencias.IAlmacenLicencia>(_ =>
    new Resumenes.Ui.Servicios.Licencia.AlmacenLicenciaDpapi(
        System.IO.Path.Combine(raizDatos, "licencia.dat")));
sc.AddSingleton<Resumenes.Core.Licencias.IClienteLicencias>(_ =>
    new Resumenes.Ui.Servicios.Licencia.ClienteLicenciasHttp(
        new HttpClient { Timeout = TimeSpan.FromSeconds(15) },
        "https://resumenes-production.up.railway.app"));
sc.AddSingleton<Resumenes.Core.Licencias.ValidadorTokenLicencia>();
sc.AddSingleton<Resumenes.Ui.Servicios.Licencia.ServicioLicencia>();
sc.AddTransient<ActivacionVm>();
sc.AddTransient<Resumenes.Ui.Vistas.VentanaActivacion>();
```

Reemplazar el bloque que muestra la ventana principal (`Servicios.GetRequiredService<MainWindow>().Show();`) por el **gate**:

```csharp
// -------- Gate de licencia --------
var servicioLicencia = Servicios.GetRequiredService<Resumenes.Ui.Servicios.Licencia.ServicioLicencia>();
// Task.Run saca la continuación del SynchronizationContext de WPF y evita el deadlock
// clásico de async-sobre-sync al bloquear el hilo de UI durante el arranque.
var estadoLic = System.Threading.Tasks.Task.Run(() =>
    servicioLicencia.ObtenerEstadoAsync(System.Threading.CancellationToken.None)).GetAwaiter().GetResult();

if (estadoLic is Resumenes.Core.Licencias.EstadoLicenciaCliente.Activa)
{
    var main = Servicios.GetRequiredService<MainWindow>();
    MainWindow = main;
    main.Show();
}
else
{
    if (estadoLic is Resumenes.Core.Licencias.EstadoLicenciaCliente.BloqueadaPorGracia)
        MessageBox.Show(
            "Tu licencia necesita reconectarse a internet para seguir usándose. " +
            "Conectate y volvé a abrir la app.",
            "Resúmenes — Licencia", MessageBoxButton.OK, MessageBoxImage.Warning);

    var ventana = Servicios.GetRequiredService<Resumenes.Ui.Vistas.VentanaActivacion>();
    MainWindow = ventana;
    ventana.Show();
}
```

> Nota: `ObtenerEstadoAsync` hace la validación offline (instantánea) y solo va a la red si toca revalidar (cada 14 días); el `HttpClient` tiene timeout de 15 s, así que el peor caso de arranque es acotado. El bloque `--demo` y `base.OnStartup(e)` quedan igual.

- [ ] **Step 4: Compilar y correr la suite completa de Ui**

Run: `dotnet build src/Resumenes.Ui` y `dotnet test tests/Resumenes.Ui.Tests`
Expected: BUILD SUCCEEDED; todos los tests verdes.

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Ui/Vistas/VentanaActivacion.xaml src/Resumenes.Ui/Vistas/VentanaActivacion.xaml.cs src/Resumenes.Ui/App.xaml.cs
git commit -m "feat(licencias-cliente): ventana de activacion + gate de arranque + DI"
```

---

## Task 10: Verificación manual end-to-end + release

**Files:**
- Modify: `src/Resumenes.Ui/Resumenes.Ui.csproj` (`<Version>` → `1.2.0`)
- Modify: `installer/Resumenes.iss` (`MyAppVersion` → `1.2.0`)
- Modify: `CHANGELOG.md`

**Interfaces:** producto final con gate de licencia.

- [ ] **Step 1: Smoke manual del gate (sin licencia)**

Borrá el archivo de licencia si existe y corré la app:
```bash
del "%LOCALAPPDATA%\ResumenesApp\licencia.dat"
dotnet run --project src/Resumenes.Ui
```
Expected: arranca la **VentanaActivacion** (no la app). Ingresá una clave creada en la API (`POST /admin/licencias`) → Activar → se abre la app. Cerrá y reabrí: entra directo (token guardado).

- [ ] **Step 2: Subir la versión**

En `src/Resumenes.Ui/Resumenes.Ui.csproj`: `<Version>1.1.0</Version>` → `<Version>1.2.0</Version>`.
En `installer/Resumenes.iss`: `#define MyAppVersion "1.1.0"` → `"1.2.0"`.

- [ ] **Step 3: Nota de cambios**

En `CHANGELOG.md`, agregar una sección `## [1.2.0] — 2026-06-24` con: "Activación por licencia: la app ahora requiere una clave de activación válida (verificada contra el servidor, con revalidación periódica y tolerancia offline de 30 días)." Agregar el link `[1.2.0]` al pie.

- [ ] **Step 4: Build de release + suite completa**

Run: `dotnet build -c Release` y `dotnet test`
Expected: BUILD SUCCEEDED; toda la suite verde.

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Ui/Resumenes.Ui.csproj installer/Resumenes.iss CHANGELOG.md
git commit -m "chore(licencias-cliente): version 1.2.0 + CHANGELOG (gate de activacion)"
```

---

## Cierre de la Fase B–E

Al terminar: la app queda bloqueada hasta activar con una clave válida; tras activar funciona offline con revalidación cada 14 días y gracia de 30; el token se guarda cifrado con DPAPI atado al usuario+equipo.

**Pasos manuales del usuario (fuera del código):**
1. Generar el instalador 1.2.0 y publicarlo (igual que la 1.1.0).
2. Para vender: crear una licencia con `POST /admin/licencias` (con la `ADMIN_KEY`) y enviarle la clave al comprador.

**Mejoras futuras (backlog, no en este plan):** revalidación en background con aviso no intrusivo (en vez de en el arranque); endpoint para borrar licencias; "liberar mi equipo" desde la propia app; ofuscación del binario.
