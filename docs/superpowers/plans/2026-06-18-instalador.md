# Instalador — Plan de implementación

> **Para workers agénticos:** SUB-SKILL REQUERIDA: usar superpowers:subagent-driven-development (recomendado) o superpowers:executing-plans, tarea por tarea. Los pasos usan checkbox (`- [ ]`).

**Goal:** Instalador Inno Setup liviano + descargador in-app que baja las dependencias pesadas (Python, LibreOffice, modelos) por manifest, con progreso, dejando todo listo para construir y publicar.

**Architecture:** Instalador chico (app .NET framework-dependent + scripts + fuentes + settings) → primer arranque → Onboarding corre `IDescargadorDependencias`, que lee un `manifest.json` (host-agnóstico) y baja+verifica(SHA-256)+descomprime cada bundle a una carpeta por-usuario escribible. Entregables de empaquetado: `installer/Resumenes.iss`, `installer/build-bundles.ps1`, `installer/manifest.template.json`, `installer/README.md`.

**Tech Stack:** .NET 9 (`net9.0` backbone, `net9.0-windows` UI), C#, System.Text.Json, System.IO.Compression, System.Security.Cryptography (SHA-256), HttpClient, WPF + WPF-UI, Inno Setup 6.1+, PowerShell.

## Global Constraints

- **SIN git** (regla del proyecto `no-git-en-resumenes`): NO `git add`/`git commit`/worktrees. El paso de cierre de cada tarea es **build + tests en verde** y actualizar el ledger `docs/superpowers/plans/progress-instalador.md`.
- Todo bajo `D:\Desarrollo\Programacion\Resumenes`. Responder en español.
- Backbone targetea `net9.0`; la UI `net9.0-windows`. La máquina tiene SDK 9.0.200 (NO .NET 10).
- Tests con **xUnit**. Tests de Core/Infra en `tests/Resumenes.Tests`; tests de VM en `tests/Resumenes.Ui.Tests`.
- **Instalación por-usuario**: app en `%LOCALAPPDATA%\Programs\ResumenesApp`; runtime descargado en `%LOCALAPPDATA%\ResumenesApp\runtime\{python,libreoffice}`; modelos PaddleOCR en `%USERPROFILE%\.paddlex\official_models` (la cache que PaddleOCR ya lee).
- **Integridad**: ningún bundle se usa sin verificar SHA-256. Descargas reanudables (HTTP Range). Idempotencia por marcador `.bundle-ok` con el SHA-256.
- **Manifest host-agnóstico**: claves JSON en minúscula (`id,url,sha256,bytes,destino,tipo`). `destino` admite variables de entorno (`%USERPROFILE%`); si queda relativo, es relativo a la raíz de runtime por-usuario.
- No exponer la API key. No reescribir el worker de OCR (los modelos se proveen vía cache `~/.paddlex`).

## File Structure

| Archivo | Responsabilidad |
|---|---|
| `src/Resumenes.Core/Interfaces/IDescargadorDependencias.cs` (crear) | Contrato + `EstadoDescarga`, `FaseDescarga`, `ResultadoDescarga` |
| `src/Resumenes.Infrastructure/Instalador/DescargadorDependencias.cs` (crear) | Implementación: manifest, descarga+Range, SHA-256, descompresión, idempotencia |
| `src/Resumenes.Infrastructure/Aplicacion/Configuracion.cs` (modificar) | `ManifestUrl`, `RutaRuntime` |
| `src/Resumenes.Ui/App.xaml.cs` (modificar) | Expandir env-vars en rutas, calcular `RutaRuntime` por defecto, registrar `IDescargadorDependencias` en DI |
| `src/Resumenes.Ui/ViewModels/OnboardingVm.cs` (modificar) | Inyectar descargador, comando + propiedades de progreso |
| `src/Resumenes.Ui/Vistas/VistaOnboarding.xaml` (modificar) | Botón cableado + área de progreso |
| `tests/Resumenes.Tests/DescargadorDependenciasTests.cs` (crear) | Tests del descargador (handler HTTP fake) |
| `tests/Resumenes.Ui.Tests/OnboardingVmTests.cs` (crear) | Test del comando de descarga con descargador fake |
| `installer/Resumenes.iss` (crear) | Script Inno Setup |
| `installer/build-bundles.ps1` (crear) | Arma los 3 zips, calcula SHA-256, genera manifest |
| `installer/manifest.template.json` (crear) | Plantilla del manifest |
| `installer/README.md` (crear) | Cómo publicar |
| `config/settings.instalacion.json` (crear) | settings que deja el instalador (rutas por-usuario) |
| `docs/superpowers/plans/progress-instalador.md` (crear) | Ledger de progreso (sin git) |

---

### Task 1: Contratos del descargador (Core)

**Files:**
- Create: `src/Resumenes.Core/Interfaces/IDescargadorDependencias.cs`
- Test: (sin test propio; es solo el contrato — se ejercita en Task 3)

**Interfaces:**
- Produces: `IDescargadorDependencias.DescargarFaltantesAsync(IProgress<EstadoDescarga>?, CancellationToken) : Task<ResultadoDescarga>`; `enum FaseDescarga`; `record EstadoDescarga`; `record ResultadoDescarga`.

- [ ] **Step 1: Crear el archivo con los contratos**

```csharp
namespace Resumenes.Core.Interfaces;

/// <summary>Fase actual de la descarga de un bundle.</summary>
public enum FaseDescarga { LeyendoManifest, Descargando, Verificando, Descomprimiendo, Completado, Error }

/// <summary>Estado de progreso reportado durante la descarga.</summary>
public record EstadoDescarga(
    string BundleId, FaseDescarga Fase, long BytesActual, long BytesTotal,
    int BundleIndice, int BundleTotal, string Detalle);

/// <summary>Resultado agregado de descargar todos los bundles faltantes.</summary>
public record ResultadoDescarga(int Ok, int Salteados, int Errores, IReadOnlyList<string> Fallos);

/// <summary>Descarga, verifica y descomprime las dependencias externas según un manifest.</summary>
public interface IDescargadorDependencias
{
    Task<ResultadoDescarga> DescargarFaltantesAsync(IProgress<EstadoDescarga>? progreso, CancellationToken ct);
}
```

- [ ] **Step 2: Compilar el proyecto Core**

Run: `dotnet build src/Resumenes.Core/Resumenes.Core.csproj -c Debug --nologo`
Expected: `Compilación correcta. 0 Errores`.

- [ ] **Step 3 (cierre):** marcar Task 1 en `progress-instalador.md`.

---

### Task 2: Configuración (ManifestUrl + RutaRuntime)

**Files:**
- Modify: `src/Resumenes.Infrastructure/Aplicacion/Configuracion.cs`
- Test: `tests/Resumenes.Tests/ConfiguracionTests.cs` (crear)

**Interfaces:**
- Produces: `Configuracion.ManifestUrl` (string), `Configuracion.RutaRuntime` (string).

- [ ] **Step 1: Test de defaults**

Crear `tests/Resumenes.Tests/ConfiguracionTests.cs`:
```csharp
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
```

- [ ] **Step 2: Correr el test (falla)**

Run: `dotnet test tests/Resumenes.Tests/Resumenes.Tests.csproj --filter Defaults_incluyen_ManifestUrl_y_RutaRuntime --nologo`
Expected: FAIL (no existen las propiedades).

- [ ] **Step 3: Agregar las propiedades**

En `Configuracion.cs`, dentro de la clase `Configuracion`, agregar:
```csharp
    /// <summary>URL del manifest.json con los bundles a descargar. Editable según el host.</summary>
    public string ManifestUrl { get; set; } = "https://example.com/resumenes/manifest.json";
    /// <summary>Raíz por-usuario donde se descomprime el runtime. Vacío = App calcula %LOCALAPPDATA%/ResumenesApp/runtime.</summary>
    public string RutaRuntime { get; set; } = "";
```

- [ ] **Step 4: Correr el test (pasa)**

Run: `dotnet test tests/Resumenes.Tests/Resumenes.Tests.csproj --filter Defaults_incluyen_ManifestUrl_y_RutaRuntime --nologo`
Expected: PASS.

- [ ] **Step 5 (cierre):** marcar Task 2 en el ledger.

---

### Task 3: DescargadorDependencias (Infra) — TDD

**Files:**
- Create: `src/Resumenes.Infrastructure/Instalador/DescargadorDependencias.cs`
- Test: `tests/Resumenes.Tests/DescargadorDependenciasTests.cs`

**Interfaces:**
- Consumes: `IDescargadorDependencias`, `EstadoDescarga`, `ResultadoDescarga` (Task 1).
- Produces: `class DescargadorDependencias(HttpClient http, string manifestUrl, string raizRuntime) : IDescargadorDependencias`.

- [ ] **Step 1: Escribir los tests con un HttpMessageHandler fake**

Crear `tests/Resumenes.Tests/DescargadorDependenciasTests.cs`:
```csharp
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using Resumenes.Infrastructure.Instalador;
using Xunit;

namespace Resumenes.Tests;

public class DescargadorDependenciasTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), $"desc_{Guid.NewGuid():N}");

    // Handler fake: sirve un manifest y un zip; soporta Range (206) para probar reanudación.
    private sealed class FakeHandler(byte[] zip, Func<string> manifest) : HttpMessageHandler
    {
        public int VecesZip;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            if (req.RequestUri!.AbsoluteUri.EndsWith("manifest.json"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(manifest()) });

            VecesZip++;
            var range = req.Headers.Range?.Ranges.FirstOrDefault();
            if (range?.From is long from)
            {
                var resto = zip[(int)from..];
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(resto) });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(zip) });
        }
    }

    private static byte[] CrearZipConArchivo(string nombre, string contenido)
    {
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var e = z.CreateEntry(nombre);
            using var w = new StreamWriter(e.Open());
            w.Write(contenido);
        }
        return ms.ToArray();
    }

    private static string Sha256(byte[] b) => Convert.ToHexString(SHA256.HashData(b));

    private string Manifest(string sha) =>
        $$"""
        { "version":"1.0.0", "bundles":[
          { "id":"demo","url":"http://test/demo.zip","sha256":"{{sha}}","bytes":{{0}},"destino":"runtime/demo","tipo":"zip" }
        ]}
        """;

    [Fact]
    public async Task Descarga_verifica_y_descomprime_un_bundle()
    {
        var zip = CrearZipConArchivo("hola.txt", "mundo");
        var http = new HttpClient(new FakeHandler(zip, () => Manifest(Sha256(zip))));
        var d = new DescargadorDependencias(http, "http://test/manifest.json", _tmp);

        var r = await d.DescargarFaltantesAsync(null, CancellationToken.None);

        Assert.Equal(1, r.Ok);
        Assert.Equal(0, r.Errores);
        Assert.Equal("mundo", File.ReadAllText(Path.Combine(_tmp, "runtime", "demo", "hola.txt")));
        Assert.True(File.Exists(Path.Combine(_tmp, "runtime", "demo", ".bundle-ok")));
    }

    [Fact]
    public async Task Saltea_si_ya_esta_instalado_con_sha_valido()
    {
        var zip = CrearZipConArchivo("hola.txt", "mundo");
        var handler = new FakeHandler(zip, () => Manifest(Sha256(zip)));
        var http = new HttpClient(handler);
        var d = new DescargadorDependencias(http, "http://test/manifest.json", _tmp);

        await d.DescargarFaltantesAsync(null, CancellationToken.None);
        var r2 = await d.DescargarFaltantesAsync(null, CancellationToken.None);

        Assert.Equal(1, r2.Salteados);
        Assert.Equal(1, handler.VecesZip); // no se re-descargó
    }

    [Fact]
    public async Task Sha_invalido_reporta_error_y_no_descomprime()
    {
        var zip = CrearZipConArchivo("hola.txt", "mundo");
        var http = new HttpClient(new FakeHandler(zip, () => Manifest("00DEADBEEF")));
        var d = new DescargadorDependencias(http, "http://test/manifest.json", _tmp);

        var r = await d.DescargarFaltantesAsync(null, CancellationToken.None);

        Assert.Equal(1, r.Errores);
        Assert.False(Directory.Exists(Path.Combine(_tmp, "runtime", "demo")));
    }

    public void Dispose() { if (Directory.Exists(_tmp)) Directory.Delete(_tmp, true); }
}
```

- [ ] **Step 2: Correr (falla por falta de clase)**

Run: `dotnet test tests/Resumenes.Tests/Resumenes.Tests.csproj --filter DescargadorDependenciasTests --nologo`
Expected: FAIL (no existe `DescargadorDependencias`).

- [ ] **Step 3: Implementar el descargador**

Crear `src/Resumenes.Infrastructure/Instalador/DescargadorDependencias.cs`:
```csharp
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.Instalador;

public class DescargadorDependencias(HttpClient http, string manifestUrl, string raizRuntime) : IDescargadorDependencias
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<ResultadoDescarga> DescargarFaltantesAsync(IProgress<EstadoDescarga>? progreso, CancellationToken ct)
    {
        progreso?.Report(new EstadoDescarga("", FaseDescarga.LeyendoManifest, 0, 0, 0, 0, "Leyendo manifest…"));
        var json = await http.GetStringAsync(manifestUrl, ct);
        var manifest = JsonSerializer.Deserialize<ManifestDescarga>(json, JsonOpts)
            ?? throw new InvalidOperationException("Manifest inválido o vacío.");

        int ok = 0, salt = 0, err = 0;
        var fallos = new List<string>();
        var bundles = manifest.Bundles ?? new();

        for (int i = 0; i < bundles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var b = bundles[i];
            var destino = ResolverDestino(b.Destino);
            int idx = i + 1, total = bundles.Count;
            try
            {
                if (YaInstalado(destino, b.Sha256))
                {
                    salt++;
                    progreso?.Report(new EstadoDescarga(b.Id, FaseDescarga.Completado, b.Bytes, b.Bytes, idx, total, "Ya instalado"));
                    continue;
                }

                var zipPath = destino + ".part";
                await DescargarConReanudacionAsync(b, zipPath, idx, total, progreso, ct);

                progreso?.Report(new EstadoDescarga(b.Id, FaseDescarga.Verificando, b.Bytes, b.Bytes, idx, total, "Verificando…"));
                var hash = await Sha256Async(zipPath, ct);
                if (!string.Equals(hash, b.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(zipPath);
                    throw new InvalidOperationException($"SHA-256 no coincide (esperado {b.Sha256}, obtenido {hash}).");
                }

                progreso?.Report(new EstadoDescarga(b.Id, FaseDescarga.Descomprimiendo, b.Bytes, b.Bytes, idx, total, "Descomprimiendo…"));
                if (Directory.Exists(destino)) Directory.Delete(destino, true);
                Directory.CreateDirectory(destino);
                ZipFile.ExtractToDirectory(zipPath, destino, overwriteFiles: true);
                File.Delete(zipPath);
                File.WriteAllText(Path.Combine(destino, ".bundle-ok"), b.Sha256);

                ok++;
                progreso?.Report(new EstadoDescarga(b.Id, FaseDescarga.Completado, b.Bytes, b.Bytes, idx, total, "Listo"));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                err++;
                fallos.Add($"{b.Id}: {ex.Message}");
                progreso?.Report(new EstadoDescarga(b.Id, FaseDescarga.Error, 0, b.Bytes, idx, total, ex.Message));
            }
        }
        return new ResultadoDescarga(ok, salt, err, fallos);
    }

    private string ResolverDestino(string destino)
    {
        var exp = Environment.ExpandEnvironmentVariables(destino ?? "");
        return Path.IsPathRooted(exp) ? exp : Path.Combine(raizRuntime, exp);
    }

    private static bool YaInstalado(string destino, string sha)
    {
        var marcador = Path.Combine(destino, ".bundle-ok");
        return File.Exists(marcador) &&
               string.Equals(File.ReadAllText(marcador).Trim(), sha, StringComparison.OrdinalIgnoreCase);
    }

    private async Task DescargarConReanudacionAsync(BundleDescarga b, string zipPath, int idx, int total,
        IProgress<EstadoDescarga>? progreso, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
        long yaTengo = File.Exists(zipPath) ? new FileInfo(zipPath).Length : 0;
        if (b.Bytes > 0 && yaTengo > b.Bytes) { File.Delete(zipPath); yaTengo = 0; }

        using var req = new HttpRequestMessage(HttpMethod.Get, b.Url);
        if (yaTengo > 0) req.Headers.Range = new RangeHeaderValue(yaTengo, null);

        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        bool reanuda = resp.StatusCode == HttpStatusCode.PartialContent && yaTengo > 0;
        if (!reanuda) yaTengo = 0;
        resp.EnsureSuccessStatusCode();

        await using var entrada = await resp.Content.ReadAsStreamAsync(ct);
        await using var salida = new FileStream(zipPath, reanuda ? FileMode.Append : FileMode.Create,
            FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long descargado = yaTengo;
        int n;
        while ((n = await entrada.ReadAsync(buffer, ct)) > 0)
        {
            await salida.WriteAsync(buffer.AsMemory(0, n), ct);
            descargado += n;
            progreso?.Report(new EstadoDescarga(b.Id, FaseDescarga.Descargando, descargado, b.Bytes, idx, total,
                $"{descargado / 1_048_576} / {(b.Bytes > 0 ? b.Bytes / 1_048_576 : 0)} MB"));
        }
    }

    private static async Task<string> Sha256Async(string path, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        await using var fs = File.OpenRead(path);
        return Convert.ToHexString(await sha.ComputeHashAsync(fs, ct));
    }
}

internal record ManifestDescarga(string Version, List<BundleDescarga> Bundles);
internal record BundleDescarga(string Id, string Url, string Sha256, long Bytes, string Destino, string Tipo);
```

- [ ] **Step 4: Correr los tests (pasan)**

Run: `dotnet test tests/Resumenes.Tests/Resumenes.Tests.csproj --filter DescargadorDependenciasTests --nologo`
Expected: PASS (3/3).

- [ ] **Step 5 (cierre):** marcar Task 3 en el ledger.

---

### Task 4: DI + rutas por-usuario (App.xaml.cs)

**Files:**
- Modify: `src/Resumenes.Ui/App.xaml.cs`

**Interfaces:**
- Consumes: `IDescargadorDependencias`, `DescargadorDependencias`, `Configuracion.ManifestUrl/RutaRuntime`.
- Produces: `IDescargadorDependencias` registrado en DI; `RutaRuntime` calculado; rutas de config con env-vars expandidas.

- [ ] **Step 1: Calcular RutaRuntime + expandir env-vars en la carga de config**

En `App.OnStartup`, justo después de cargar `cfg` y antes de registrar adaptadores, agregar:
```csharp
        // Runtime por-usuario (escribible) donde se descomprimen Python/LibreOffice.
        var raizDatos = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ResumenesApp");
        if (string.IsNullOrWhiteSpace(cfg.RutaRuntime))
            cfg.RutaRuntime = System.IO.Path.Combine(raizDatos, "runtime");
        cfg.RutaRuntime = Environment.ExpandEnvironmentVariables(cfg.RutaRuntime);

        // Expandir variables de entorno en las rutas (settings de instalación las usa).
        cfg.PythonExe       = Environment.ExpandEnvironmentVariables(cfg.PythonExe);
        cfg.LibreOfficeDir  = Environment.ExpandEnvironmentVariables(cfg.LibreOfficeDir);
        cfg.ModelosPaddle   = Environment.ExpandEnvironmentVariables(cfg.ModelosPaddle);
        cfg.ScriptsDir      = Environment.ExpandEnvironmentVariables(cfg.ScriptsDir);
        cfg.FontsDir        = Environment.ExpandEnvironmentVariables(cfg.FontsDir);
        cfg.ManifestUrl     = Environment.ExpandEnvironmentVariables(cfg.ManifestUrl);
```

- [ ] **Step 2: Registrar el descargador en DI**

En la sección de servicios de UI de `App.OnStartup` (junto a `sc.AddSingleton<ServicioNavegacion>();`), agregar:
```csharp
        sc.AddSingleton<Resumenes.Core.Interfaces.IDescargadorDependencias>(_ =>
            new Resumenes.Infrastructure.Instalador.DescargadorDependencias(
                new HttpClient { Timeout = TimeSpan.FromMinutes(60) }, cfg.ManifestUrl, cfg.RutaRuntime));
```
(Agregar `using Resumenes.Infrastructure.Instalador;` no es necesario si se usa el nombre completo como arriba.)

- [ ] **Step 3: Compilar la UI**

Run: `dotnet build src/Resumenes.Ui/Resumenes.Ui.csproj -c Debug --nologo`
Expected: `Compilación correcta. 0 Errores`.

- [ ] **Step 4 (cierre):** marcar Task 4 en el ledger.

---

### Task 5: OnboardingVm (comando de descarga + progreso) — TDD

**Files:**
- Modify: `src/Resumenes.Ui/ViewModels/OnboardingVm.cs`
- Test: `tests/Resumenes.Ui.Tests/OnboardingVmTests.cs` (crear)

**Interfaces:**
- Consumes: `IDescargadorDependencias`, `EstadoDescarga`, `ResultadoDescarga`.
- Produces: `OnboardingVm` con ctor `(IAlmacenSecretos, Configuracion, ServicioNavegacion, IDescargadorDependencias)`, propiedades `Descargando` (bool), `TextoProgreso` (string), `FraccionGlobal` (double), y `DescargarDependenciasCommand`.

- [ ] **Step 1: Escribir el test con un descargador fake**

Crear `tests/Resumenes.Ui.Tests/OnboardingVmTests.cs`:
```csharp
using Resumenes.Core.Interfaces;
using Resumenes.Infrastructure.Aplicacion;
using Resumenes.Ui.Servicios;
using Resumenes.Ui.ViewModels;
using Xunit;

namespace Resumenes.Ui.Tests;

public class OnboardingVmTests
{
    private class SecretosFake : IAlmacenSecretos
    {
        public string? ObtenerApiKey() => "sk-x";
        public void GuardarApiKey(string key) { }
    }

    private class DescargadorFake : IDescargadorDependencias
    {
        public Task<ResultadoDescarga> DescargarFaltantesAsync(IProgress<EstadoDescarga>? p, CancellationToken ct)
        {
            p?.Report(new EstadoDescarga("python", FaseDescarga.Descargando, 50, 100, 1, 2, "50 / 100 MB"));
            p?.Report(new EstadoDescarga("python", FaseDescarga.Completado, 100, 100, 1, 2, "Listo"));
            return Task.FromResult(new ResultadoDescarga(2, 0, 0, System.Array.Empty<string>()));
        }
    }

    [Fact]
    public async Task DescargarDependencias_actualiza_progreso_y_termina()
    {
        var vm = new OnboardingVm(new SecretosFake(), new Configuracion(), new ServicioNavegacion(), new DescargadorFake());
        await vm.DescargarDependenciasCommand.ExecuteAsync(null);

        Assert.False(vm.Descargando);                 // terminó
        Assert.Contains("100", vm.TextoProgreso);     // reflejó el último progreso
        Assert.Equal(1.0, vm.FraccionGlobal, 3);      // 2 de 2 bundles
    }
}
```

- [ ] **Step 2: Correr (falla)**

Run: `dotnet test tests/Resumenes.Ui.Tests/Resumenes.Ui.Tests.csproj --filter DescargarDependencias_actualiza_progreso_y_termina --nologo`
Expected: FAIL (ctor/propiedades/command no existen).

- [ ] **Step 3: Modificar OnboardingVm**

En `OnboardingVm.cs`: (a) agregar usings `using System.Threading;` (si falta) y `using Resumenes.Core.Interfaces;` (ya está). (b) Inyectar el descargador y agregar propiedades + comando:
```csharp
    private readonly IDescargadorDependencias _descargador;

    [ObservableProperty] private bool _descargando;
    [ObservableProperty] private string _textoProgreso = string.Empty;
    [ObservableProperty] private double _fraccionGlobal;

    // Reemplazar el ctor existente por:
    public OnboardingVm(IAlmacenSecretos secretos, Configuracion cfg, ServicioNavegacion nav,
        IDescargadorDependencias descargador)
    {
        _secretos = secretos;
        _cfg = cfg;
        _nav = nav;
        _descargador = descargador;
        Verificar();
    }

    /// <summary>Descarga las dependencias faltantes mostrando progreso; al terminar re-verifica.</summary>
    [RelayCommand]
    private async Task DescargarDependencias()
    {
        if (Descargando) return;
        Descargando = true;
        TextoProgreso = "Iniciando…";
        FraccionGlobal = 0;
        try
        {
            var progreso = new Progress<EstadoDescarga>(e =>
            {
                TextoProgreso = $"{e.BundleId} — {e.Detalle}";
                if (e.BundleTotal > 0)
                {
                    double fracBundle = e.BytesTotal > 0 ? (double)e.BytesActual / e.BytesTotal : (e.Fase == FaseDescarga.Completado ? 1 : 0);
                    FraccionGlobal = ((e.BundleIndice - 1) + fracBundle) / e.BundleTotal;
                }
            });
            var r = await _descargador.DescargarFaltantesAsync(progreso, CancellationToken.None);
            FraccionGlobal = 1.0;
            TextoProgreso = r.Errores == 0
                ? $"Descarga completa ({r.Ok} listo(s), {r.Salteados} ya estaban)."
                : $"Con {r.Errores} error(es): {string.Join(" | ", r.Fallos)}";
            Verificar();
        }
        catch (System.Exception ex)
        {
            TextoProgreso = $"Error: {ex.Message}";
        }
        finally
        {
            Descargando = false;
        }
    }
```

- [ ] **Step 4: Correr el test (pasa)**

Run: `dotnet test tests/Resumenes.Ui.Tests/Resumenes.Ui.Tests.csproj --filter DescargarDependencias_actualiza_progreso_y_termina --nologo`
Expected: PASS.

- [ ] **Step 5: Verificar que DI siga resolviendo OnboardingVm**

`OnboardingVm` se registra como `sc.AddTransient<OnboardingVm>();` (ctor por reflexión): el nuevo parámetro `IDescargadorDependencias` ya está registrado (Task 4), así que no hay cambio de registro. Compilar la solución:
Run: `dotnet build Resumenes.sln -c Debug --nologo`
Expected: `0 Errores`.

- [ ] **Step 6 (cierre):** marcar Task 5 en el ledger.

---

### Task 6: VistaOnboarding.xaml (botón + progreso)

**Files:**
- Modify: `src/Resumenes.Ui/Vistas/VistaOnboarding.xaml`

**Interfaces:**
- Consumes: `DescargarDependenciasCommand`, `Descargando`, `TextoProgreso`, `FraccionGlobal` (Task 5).

- [ ] **Step 1: Leer la vista actual**

Run: leer `src/Resumenes.Ui/Vistas/VistaOnboarding.xaml` para ubicar el botón de descarga placeholder (usa `Symbol=ArrowDownload24`) y los conversores ya disponibles.

- [ ] **Step 2: Cablear el botón y agregar el área de progreso**

Reemplazar el botón placeholder de descarga por uno enlazado al comando, y agregar debajo un bloque de progreso. Snippet a insertar (ajustar al layout existente):
```xml
<ui:Button Content="Descargar dependencias"
           Command="{Binding DescargarDependenciasCommand}"
           Appearance="Primary"
           Icon="{ui:SymbolIcon Symbol=ArrowDownload24}"/>

<!-- Progreso (visible mientras descarga) -->
<StackPanel Margin="0,12,0,0"
            Visibility="{Binding Descargando, Converter={StaticResource BoolToVis}}">
  <ProgressBar Minimum="0" Maximum="1" Value="{Binding FraccionGlobal}" Height="8"/>
  <TextBlock Text="{Binding TextoProgreso}" FontSize="12"
             Foreground="{DynamicResource TextFillColorSecondaryBrush}"
             Margin="0,6,0,0" TextWrapping="Wrap"/>
</StackPanel>
```
Nota: NO hace falta `IsEnabled`: el `[RelayCommand]` genera un `AsyncRelayCommand` que se deshabilita solo mientras se ejecuta (no permite ejecuciones concurrentes por defecto). Asegurar que `BoolToVis` esté en `Page.Resources` (mismo patrón que las otras vistas: `<BooleanToVisibilityConverter x:Key="BoolToVis"/>`).

- [ ] **Step 3: Compilar y verificar visualmente**

Run: `dotnet build src/Resumenes.Ui/Resumenes.Ui.csproj -c Debug --nologo` → `0 Errores`.
Verificación manual: lanzar la app sin runtime (renombrar temporalmente la carpeta runtime), llegar a Onboarding, ver el botón y (al clickear, si hay manifest de prueba) la barra de progreso. (La descarga real se prueba end-to-end en Task 9.)

- [ ] **Step 4 (cierre):** marcar Task 6 en el ledger.

---

### Task 7: Inno Setup (installer/Resumenes.iss) + publish

**Files:**
- Create: `installer/Resumenes.iss`
- Create: `config/settings.instalacion.json`

- [ ] **Step 1: settings de instalación (rutas por-usuario)**

Crear `config/settings.instalacion.json`:
```json
{
  "RutaWorkspace": "%LOCALAPPDATA%\\ResumenesApp\\workspace",
  "RutaRuntime": "%LOCALAPPDATA%\\ResumenesApp\\runtime",
  "PythonExe": "%LOCALAPPDATA%\\ResumenesApp\\runtime\\python\\python.exe",
  "ScriptsDir": "runtime\\scripts",
  "ModelosPaddle": "%USERPROFILE%\\.paddlex\\official_models",
  "FontsDir": "runtime\\fonts",
  "LibreOfficeDir": "%LOCALAPPDATA%\\ResumenesApp\\runtime\\libreoffice",
  "Dpi": 200,
  "MaxCharsIA": 16000,
  "Modelo": "deepseek-v4-flash",
  "BaseUrlDeepseek": "https://api.deepseek.com",
  "ManifestUrl": "PEGAR_URL_DEL_MANIFEST_AQUI"
}
```
(`ScriptsDir`/`FontsDir` quedan relativos al exe = se resuelven junto a la app instalada.)

- [ ] **Step 2: Publicar la app framework-dependent**

Run: `dotnet publish src/Resumenes.Ui/Resumenes.Ui.csproj -c Release -r win-x64 --self-contained false -o publish/app --nologo`
Expected: genera `publish/app/Resumenes.Ui.exe` + dlls (sin runtime .NET embebido).

- [ ] **Step 3: Crear el script Inno**

Crear `installer/Resumenes.iss`:
```iss
#define MyAppName "Resúmenes de Estudio"
#define MyAppExe "Resumenes.Ui.exe"
#define MyAppVersion "1.0.0"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\ResumenesApp
DefaultGroupName=Resúmenes de Estudio
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=ResumenesSetup
Compression=lzma2/max
SolidCompression=yes
DisableProgramGroupPage=yes
WizardStyle=modern

[Files]
; App publicada (framework-dependent)
Source: "..\publish\app\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
; Scripts y fuentes (livianos, van en el instalador)
Source: "..\runtime\scripts\*"; DestDir: "{app}\runtime\scripts"; Flags: recursesubdirs ignoreversion
Source: "..\runtime\fonts\*"; DestDir: "{app}\runtime\fonts"; Flags: recursesubdirs ignoreversion
; settings de instalación -> config\settings.json
Source: "..\config\settings.instalacion.json"; DestDir: "{app}\config"; DestName: "settings.json"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Adicional:"

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Iniciar {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  NetUrl = 'https://aka.ms/dotnet/9.0/windowsdesktop-runtime-win-x64.exe';

function NetRuntimePresente(): Boolean;
var
  ResultCode: Integer;
  Salida: AnsiString;
  TmpFile: String;
begin
  // Corre 'dotnet --list-runtimes' y busca 'Microsoft.WindowsDesktop.App 9.'
  TmpFile := ExpandConstant('{tmp}\runtimes.txt');
  Result := False;
  if Exec(ExpandConstant('{cmd}'), '/C dotnet --list-runtimes > "' + TmpFile + '" 2>&1', '',
          SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if LoadStringFromFile(TmpFile, Salida) then
      Result := Pos('Microsoft.WindowsDesktop.App 9.', Salida) > 0;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  DownloadPage: TDownloadWizardPage;
  ResultCode: Integer;
  Instalador: String;
begin
  if CurStep = ssInstall then
  begin
    if not NetRuntimePresente() then
    begin
      DownloadPage := CreateDownloadPage('Runtime .NET 9', 'Descargando el runtime necesario…', nil);
      DownloadPage.Clear;
      DownloadPage.Add(NetUrl, 'windowsdesktop-runtime-9-win-x64.exe', '');
      DownloadPage.Show;
      try
        DownloadPage.Download;
        Instalador := ExpandConstant('{tmp}\windowsdesktop-runtime-9-win-x64.exe');
        Exec(Instalador, '/install /quiet /norestore', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
      finally
        DownloadPage.Hide;
      end;
    end;
  end;
end;
```

- [ ] **Step 4: Compilar el instalador (manual)**

Run (con Inno Setup instalado): `& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\Resumenes.iss`
Expected: genera `dist\ResumenesSetup.exe` de pocos MB. Verificar tamaño (< ~15 MB).

- [ ] **Step 5 (cierre):** marcar Task 7 en el ledger.

---

### Task 8: build-bundles.ps1 + manifest.template.json

**Files:**
- Create: `installer/build-bundles.ps1`
- Create: `installer/manifest.template.json`

- [ ] **Step 1: Plantilla del manifest**

Crear `installer/manifest.template.json`:
```json
{
  "version": "1.0.0",
  "bundles": [
    { "id": "python",      "url": "REEMPLAZAR_URL/python-env.zip",    "sha256": "REEMPLAZAR", "bytes": 0, "destino": "python",                              "tipo": "zip" },
    { "id": "libreoffice", "url": "REEMPLAZAR_URL/libreoffice.zip",   "sha256": "REEMPLAZAR", "bytes": 0, "destino": "libreoffice",                         "tipo": "zip" },
    { "id": "modelos",     "url": "REEMPLAZAR_URL/paddle-models.zip", "sha256": "REEMPLAZAR", "bytes": 0, "destino": "%USERPROFILE%\\.paddlex\\official_models", "tipo": "zip" }
  ]
}
```

- [ ] **Step 2: Script de armado**

Crear `installer/build-bundles.ps1`:
```powershell
# Arma los bundles, calcula SHA-256/tamaño y genera dist/manifest.json.
# Requiere: python-build-standalone (CPython portable) para el bundle Python.
param(
  [string]$RaizProyecto = (Resolve-Path "$PSScriptRoot\.."),
  [string]$Salida = "$PSScriptRoot\..\dist\bundles",
  [string]$BaseUrl = "REEMPLAZAR_URL"   # ej: https://github.com/usuario/repo/releases/download/v1.0.0
)
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $Salida | Out-Null

function ZipDir($origen, $zip) {
  if (Test-Path $zip) { Remove-Item $zip -Force }
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  [System.IO.Compression.ZipFile]::CreateFromDirectory($origen, $zip)
}
function Sha($f) { (Get-FileHash $f -Algorithm SHA256).Hash }

# 1) Python: usar python-build-standalone + pip install (relocatable).
#    Bajar de https://github.com/astral-sh/python-build-standalone/releases
#    (cpython-3.12.*-x86_64-pc-windows-msvc-install_only.tar.gz), extraer a $env:TEMP\pyenv\python,
#    y luego:
#      & "$env:TEMP\pyenv\python\python.exe" -m pip install --upgrade pip
#      & "$env:TEMP\pyenv\python\python.exe" -m pip install pymupdf paddleocr paddlepaddle fpdf2
#    Ese 'python' relocatable es lo que se zipea:
$pyEnv = "$env:TEMP\pyenv\python"
if (-not (Test-Path $pyEnv)) { Write-Host "FALTA preparar $pyEnv (ver comentarios del script)"; }
$zipPy = "$Salida\python-env.zip"
if (Test-Path $pyEnv) { ZipDir $pyEnv $zipPy }

# 2) LibreOffice: zipear runtime/libreoffice actual (ya funcional).
$zipLo = "$Salida\libreoffice.zip"
ZipDir "$RaizProyecto\runtime\libreoffice" $zipLo

# 3) Modelos PaddleOCR (cache que la app usa).
$modelos = "$env:USERPROFILE\.paddlex\official_models"
$zipMo = "$Salida\paddle-models.zip"
if (Test-Path $modelos) { ZipDir $modelos $zipMo } else { Write-Host "FALTA $modelos (corré un OCR una vez para poblarlo)" }

# 4) Generar manifest.json con sha256/bytes reales.
function Entry($id, $zip, $destino) {
  if (-not (Test-Path $zip)) { return $null }
  [ordered]@{ id=$id; url="$BaseUrl/$(Split-Path $zip -Leaf)"; sha256=(Sha $zip); bytes=(Get-Item $zip).Length; destino=$destino; tipo="zip" }
}
$bundles = @(
  (Entry "python" $zipPy "python"),
  (Entry "libreoffice" $zipLo "libreoffice"),
  (Entry "modelos" $zipMo "%USERPROFILE%\.paddlex\official_models")
) | Where-Object { $_ -ne $null }
$manifest = [ordered]@{ version="1.0.0"; bundles=$bundles }
$manifest | ConvertTo-Json -Depth 5 | Set-Content "$Salida\manifest.json" -Encoding utf8
Write-Host "Listo. Bundles + manifest en $Salida"
Write-Host "Subí los .zip a tu host, pegá la BaseUrl correcta y poné la URL de manifest.json en settings.json (ManifestUrl)."
```

- [ ] **Step 3: Verificación manual (opcional, pesado)**

Correr `installer/build-bundles.ps1` con `$pyEnv` preparado → produce `dist/bundles/*.zip` + `manifest.json` con SHA-256 reales. (Es pesado por LibreOffice; se corre al publicar, no en cada build.)

- [ ] **Step 4 (cierre):** marcar Task 8 en el ledger.

---

### Task 9: README de publicación + verificación end-to-end

**Files:**
- Create: `installer/README.md`

- [ ] **Step 1: Escribir el README**

Crear `installer/README.md` con los pasos de publicación:
```markdown
# Publicar el instalador

## 1. Publicar la app
dotnet publish src/Resumenes.Ui/Resumenes.Ui.csproj -c Release -r win-x64 --self-contained false -o publish/app

## 2. Armar bundles (una vez por versión)
- Preparar Python portable en %TEMP%\pyenv\python (python-build-standalone + pip install pymupdf paddleocr paddlepaddle fpdf2).
- Correr OCR una vez para poblar %USERPROFILE%\.paddlex\official_models.
- powershell -File installer\build-bundles.ps1 -BaseUrl "<URL-de-tus-releases>"
  → genera dist\bundles\{python-env,libreoffice,paddle-models}.zip + manifest.json

## 3. Subir
- Subí los 3 .zip al host (GitHub Releases recomendado; Drive posible con link directo + cuidado con cuota/interstitial).
- Subí manifest.json (o servilo desde el mismo host). Copiá su URL.

## 4. Configurar y compilar el instalador
- Pegá la URL del manifest en config\settings.instalacion.json (campo ManifestUrl).
- "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\Resumenes.iss  → dist\ResumenesSetup.exe

## 5. Probar en máquina/usuario limpio
- Ejecutar ResumenesSetup.exe (instala app + .NET si falta).
- Abrir la app → Onboarding → "Descargar dependencias" → progreso → al terminar, procesar un PDF de prueba.
```

- [ ] **Step 2: Self-check de consistencia**

Revisar que `ManifestUrl`, los `destino` y las rutas de `settings.instalacion.json` coincidan con lo que arma `build-bundles.ps1` (python→runtime/python, libreoffice→runtime/libreoffice, modelos→%USERPROFILE%\.paddlex\official_models).

- [ ] **Step 3: Build + tests finales de la solución**

Run: `dotnet build Resumenes.sln -c Debug --nologo` → `0 Errores`.
Run: `dotnet test Resumenes.sln --nologo` → todos verdes.

- [ ] **Step 4 (cierre):** marcar Task 9 en el ledger; el instalador queda listo para publicar.

---

## Notas de cierre

- **Verificación end-to-end real** (instalar en limpio + descargar ~1,5-2,5 GB + procesar) es manual y la hace el usuario al publicar; el plan deja todo el código + scripts listos.
- Las Tasks 1-5 son TDD y quedan cubiertas por tests automatizados; las Tasks 6-9 (XAML/Inno/PS/docs) se verifican manualmente con los comandos indicados.
