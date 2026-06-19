# Fase 1: Esqueleto end-to-end — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un runner de consola que procese una carpeta con 1 PDF chico de punta a punta (captura → OCR → limpieza IA → consolidación trivial → resumen IA → PDF), con estado idempotente en SQLite.

**Architecture:** Puertos y adaptadores liviano. `Resumenes.Core` (dominio + interfaces + orquestador, sin dependencias externas), `Resumenes.Infrastructure` (adaptadores: Deepseek, Python OCR, Python PDF, SQLite, DPAPI), `Resumenes.Cli` (composition root) y `Resumenes.Tests` (xUnit + fakes). El orquestador es una máquina de estados que recorre pasos persistiendo estado.

**Tech Stack:** .NET 9 (C#), xUnit, Microsoft.Data.Sqlite, Polly, Serilog; Python (PyMuPDF, PaddleOCR, fpdf2) como subprocesos. (Nota: la máquina tiene SDK 9.0.200; el diseño mencionaba .NET 10 pero se usa net9.0 por disponibilidad. Sin git: se omiten todos los pasos de commit/.gitignore.)

## Global Constraints

- Todo el proyecto vive bajo `D:\Desarrollo\Programacion\Resumenes`. Rutas del plan relativas a esa raíz.
- Target framework: `net9.0` en todos los proyectos.
- Idioma del código y comentarios: español (identificadores en español salvo términos técnicos).
- Encoding UTF-8 en todo archivo de texto, intercambio .NET↔Python y PDF.
- SQLite es índice de estado; los contenidos van a disco. Solo `Resumenes.Infrastructure` toca SQLite/HTTP/Process; `Resumenes.Core` no.
- Escritura de artefactos siempre atómica (`.tmp` + reemplazo).
- Identidad de archivo por SHA-256 (16 hex para `archivo_id`).
- La API key NO se guarda en `settings.json`: va cifrada con DPAPI.
- Commits frecuentes (uno por tarea como mínimo). Mensajes en imperativo.

---

## File Structure

```
Resumenes.sln
.gitignore
src/
  Resumenes.Core/
    Resumenes.Core.csproj
    Modelos/Enums.cs                 (Etapa, EstadoUnidad, EstadoAnalisis, TipoArchivo)
    Modelos/Entidades.cs             (Analisis, Archivo, Tema, Unidad)
    Interfaces/IClienteIA.cs         (+ SolicitudIA, RespuestaIA)
    Interfaces/IRasterizador.cs
    Interfaces/IServicioOcr.cs
    Interfaces/IGeneradorPdf.cs
    Interfaces/IRepositorioEstado.cs
    Interfaces/IAlmacenSecretos.cs
    Interfaces/IRelojUtc.cs
    Apoyos/Hashing.cs
    Apoyos/EscrituraAtomica.cs
    Apoyos/RelojUtcSistema.cs
    Orquestacion/PasoPipeline.cs     (+ ResultadoEjecucion)
    Orquestacion/PipelineOrquestador.cs
  Resumenes.Infrastructure/
    Resumenes.Infrastructure.csproj
    schema.sql                        (recurso embebido; copia del schema.sql raíz)
    Persistencia/SqliteRepositorioEstado.cs
    IA/DeepseekClienteIA.cs
    Ocr/ProtocoloOcr.cs               (parse NDJSON puro, testeable)
    Ocr/PaddleOcrServicio.cs
    Ocr/PyMuPdfRasterizador.cs
    Pdf/PythonGeneradorPdf.cs
    Secretos/DpapiAlmacenSecretos.cs
  Resumenes.Cli/
    Resumenes.Cli.csproj
    Configuracion.cs
    ConstructorPipeline.cs
    Program.cs
tests/
  Resumenes.Tests/
    Resumenes.Tests.csproj
    Fakes/Fakes.cs                    (FakeRepositorio, RelojFijo, etc.)
    HashingTests.cs
    EscrituraAtomicaTests.cs
    PipelineOrquestadorTests.cs
    SqliteRepositorioEstadoTests.cs
    DeepseekClienteIATests.cs
    ProtocoloOcrTests.cs
runtime/
  scripts/rasterizar.py
  scripts/worker_ocr.py
  scripts/generador_estudio_final.py  (copia del existente en la raíz)
  fonts/                              (DejaVuSans*.ttf — copiar manualmente)
tests/fixtures/
  con_tildes.pdf                      (fixture chico con ñ/tildes; agregar manualmente)
```

---

### Task 0: Bootstrap de la solución

**Files:**
- Create: `Resumenes.sln`, `.gitignore`, los 4 `.csproj`.

**Interfaces:**
- Consumes: nada.
- Produces: la solución compilable con 4 proyectos referenciados.

- [ ] **Step 1: Inicializar git (si no existe)**

```bash
cd /d/Desarrollo/Programacion/Resumenes
git init
```

- [ ] **Step 2: Crear `.gitignore`**

Create `.gitignore`:

```gitignore
bin/
obj/
*.user
.vs/
runtime/fonts/*.ttf
*.sqlite
*.sqlite-wal
*.sqlite-shm
__pycache__/
*.pdf
!tests/fixtures/*.pdf
```

- [ ] **Step 3: Crear solución y proyectos**

Run:

```bash
dotnet new sln -n Resumenes
dotnet new classlib -n Resumenes.Core -o src/Resumenes.Core -f net9.0
dotnet new classlib -n Resumenes.Infrastructure -o src/Resumenes.Infrastructure -f net9.0
dotnet new console -n Resumenes.Cli -o src/Resumenes.Cli -f net9.0
dotnet new xunit -n Resumenes.Tests -o tests/Resumenes.Tests -f net9.0
rm src/Resumenes.Core/Class1.cs src/Resumenes.Infrastructure/Class1.cs
dotnet sln add src/Resumenes.Core src/Resumenes.Infrastructure src/Resumenes.Cli tests/Resumenes.Tests
dotnet add src/Resumenes.Infrastructure reference src/Resumenes.Core
dotnet add src/Resumenes.Cli reference src/Resumenes.Core src/Resumenes.Infrastructure
dotnet add tests/Resumenes.Tests reference src/Resumenes.Core src/Resumenes.Infrastructure
```

- [ ] **Step 4: Agregar paquetes NuGet**

Run:

```bash
dotnet add src/Resumenes.Infrastructure package Microsoft.Data.Sqlite
dotnet add src/Resumenes.Infrastructure package Polly
dotnet add src/Resumenes.Infrastructure package System.Security.Cryptography.ProtectedData
dotnet add src/Resumenes.Cli package Serilog
dotnet add src/Resumenes.Cli package Serilog.Sinks.File
dotnet add src/Resumenes.Cli package Serilog.Sinks.Console
```

- [ ] **Step 5: Compilar para verificar el andamiaje**

Run: `dotnet build`
Expected: `Build succeeded` con 0 errores.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: bootstrap solucion Resumenes (4 proyectos .NET 10)"
```

---

### Task 1: Modelos del dominio (`Core`)

**Files:**
- Create: `src/Resumenes.Core/Modelos/Enums.cs`, `src/Resumenes.Core/Modelos/Entidades.cs`

**Interfaces:**
- Consumes: nada.
- Produces: `Etapa`, `EstadoUnidad`, `EstadoAnalisis`, `TipoArchivo` (enums); `Analisis`, `Archivo`, `Tema` (records inmutables); `Unidad` (clase mutable con propiedades: `Id long`, `AnalisisId string`, `ArchivoId string?`, `TemaId string?`, `Etapa`, `Estado EstadoUnidad`, `RutaArtefacto string?`, `HashEntrada string?`, `PromptVersion string?`, `ModeloIa string?`, `Tokens int?`, `FijadoPorUsuario bool`, `ErrorMsg string?`, `ActualizadoEn DateTime`).

- [ ] **Step 1: Crear los enums**

Create `src/Resumenes.Core/Modelos/Enums.cs`:

```csharp
namespace Resumenes.Core.Modelos;

public enum Etapa
{
    Captura,
    OcrBruto,
    LimpiezaIA,
    ConsolidacionTemas,
    ResumenFinal,
    GeneracionPDF
}

public enum EstadoUnidad { Pendiente, EnProceso, Completado, Error, Obsoleto }

public enum EstadoAnalisis { EnProceso, Completado, ConErrores, Obsoleto }

public enum TipoArchivo { Pdf, Doc, Docx, Ppt, Pptx, Txt, Otro }
```

- [ ] **Step 2: Crear las entidades**

Create `src/Resumenes.Core/Modelos/Entidades.cs`:

```csharp
namespace Resumenes.Core.Modelos;

public record Analisis(
    string Id,
    string Nombre,
    string CarpetaOrigen,
    string Fingerprint,
    EstadoAnalisis Estado,
    DateTime CreadoEn,
    DateTime ActualizadoEn);

public record Archivo(
    string Id,
    string AnalisisId,
    string NombreOriginal,
    string RutaRelativa,
    string HashSha256,
    long TamanoBytes,
    TipoArchivo Tipo,
    int? Paginas,
    DateTime CreadoEn);

public record Tema(
    string Id,
    string AnalisisId,
    string Nombre,
    int Orden,
    bool ConfirmadoPorUsuario);

public class Unidad
{
    public long Id { get; set; }
    public required string AnalisisId { get; set; }
    public string? ArchivoId { get; set; }
    public string? TemaId { get; set; }
    public Etapa Etapa { get; set; }
    public EstadoUnidad Estado { get; set; } = EstadoUnidad.Pendiente;
    public string? RutaArtefacto { get; set; }
    public string? HashEntrada { get; set; }
    public string? PromptVersion { get; set; }
    public string? ModeloIa { get; set; }
    public int? Tokens { get; set; }
    public bool FijadoPorUsuario { get; set; }
    public string? ErrorMsg { get; set; }
    public DateTime ActualizadoEn { get; set; }
}
```

- [ ] **Step 3: Compilar**

Run: `dotnet build src/Resumenes.Core`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add src/Resumenes.Core/Modelos
git commit -m "feat(core): modelos de dominio (enums y entidades)"
```

---

### Task 2: Interfaces del dominio (`Core`)

**Files:**
- Create: `src/Resumenes.Core/Interfaces/IClienteIA.cs`, `IRasterizador.cs`, `IServicioOcr.cs`, `IGeneradorPdf.cs`, `IRepositorioEstado.cs`, `IAlmacenSecretos.cs`, `IRelojUtc.cs`

**Interfaces:**
- Consumes: modelos de Task 1.
- Produces: las interfaces y DTOs que el resto del plan implementa. Firmas exactas abajo.

- [ ] **Step 1: Crear `IClienteIA` y sus DTOs**

Create `src/Resumenes.Core/Interfaces/IClienteIA.cs`:

```csharp
namespace Resumenes.Core.Interfaces;

public record SolicitudIA(
    string PromptSystem,
    string PromptUser,
    double Temperatura,
    int MaxTokens,
    string PromptVersion,
    string Modelo);

public record RespuestaIA(
    string Texto,
    string FinishReason,
    int TokensPrompt,
    int TokensCompletion,
    int TokensTotal);

public interface IClienteIA
{
    Task<RespuestaIA> CompletarAsync(SolicitudIA req, CancellationToken ct);
}
```

- [ ] **Step 2: Crear las interfaces de OCR, PDF, reloj y secretos**

Create `src/Resumenes.Core/Interfaces/IRasterizador.cs`:

```csharp
namespace Resumenes.Core.Interfaces;

public interface IRasterizador
{
    Task<IReadOnlyList<string>> RasterizarAsync(string pdfPath, string outDir, int dpi, CancellationToken ct);
}
```

Create `src/Resumenes.Core/Interfaces/IServicioOcr.cs`:

```csharp
namespace Resumenes.Core.Interfaces;

public interface IServicioOcr
{
    Task<string> OcrAsync(IReadOnlyList<string> rutasImagenes, CancellationToken ct);
}
```

Create `src/Resumenes.Core/Interfaces/IGeneradorPdf.cs`:

```csharp
namespace Resumenes.Core.Interfaces;

public interface IGeneradorPdf
{
    Task GenerarAsync(string contenidoPath, string pdfPath, string titulo, string subtitulo, CancellationToken ct);
}
```

Create `src/Resumenes.Core/Interfaces/IAlmacenSecretos.cs`:

```csharp
namespace Resumenes.Core.Interfaces;

public interface IAlmacenSecretos
{
    void GuardarApiKey(string key);
    string? ObtenerApiKey();
}
```

Create `src/Resumenes.Core/Interfaces/IRelojUtc.cs`:

```csharp
namespace Resumenes.Core.Interfaces;

public interface IRelojUtc
{
    DateTime Ahora();
}
```

- [ ] **Step 3: Crear `IRepositorioEstado`**

Create `src/Resumenes.Core/Interfaces/IRepositorioEstado.cs`:

```csharp
using Resumenes.Core.Modelos;

namespace Resumenes.Core.Interfaces;

public interface IRepositorioEstado
{
    void InicializarEsquema();

    Analisis? ObtenerAnalisisPorFingerprint(string fingerprint);
    void GuardarAnalisis(Analisis a);

    Archivo? ObtenerArchivo(string id);
    void GuardarArchivo(Archivo a);

    Tema? ObtenerTema(string id);
    void GuardarTema(Tema t);
    void GuardarTemaArchivo(string temaId, string archivoId);

    Unidad? ObtenerUnidad(string analisisId, string? archivoId, string? temaId, Etapa etapa);
    void GuardarUnidad(Unidad u);
}
```

- [ ] **Step 4: Compilar**

Run: `dotnet build src/Resumenes.Core`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Core/Interfaces
git commit -m "feat(core): interfaces de dominio (puertos)"
```

---

### Task 3: Utilidad `Hashing` (TDD)

**Files:**
- Create: `src/Resumenes.Core/Apoyos/Hashing.cs`, `tests/Resumenes.Tests/HashingTests.cs`

**Interfaces:**
- Produces: `static class Hashing` con `string Sha256HexDeTexto(string texto)`, `string Sha256HexDeArchivo(string ruta)`, `string ArchivoIdDesdeHash(string hashHex)` (primeros 16 chars).

- [ ] **Step 1: Escribir el test que falla**

Create `tests/Resumenes.Tests/HashingTests.cs`:

```csharp
using Resumenes.Core.Apoyos;
using Xunit;

namespace Resumenes.Tests;

public class HashingTests
{
    [Fact]
    public void Sha256DeTexto_esDeterministaYConocido()
    {
        // SHA-256 de "abc" (UTF-8) es un valor conocido.
        var hash = Hashing.Sha256HexDeTexto("abc");
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    [Fact]
    public void ArchivoId_tomaLos16PrimerosHex()
    {
        var hash = Hashing.Sha256HexDeTexto("abc");
        Assert.Equal("ba7816bf8f01cfea", Hashing.ArchivoIdDesdeHash(hash));
        Assert.Equal(16, Hashing.ArchivoIdDesdeHash(hash).Length);
    }
}
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test tests/Resumenes.Tests --filter HashingTests`
Expected: FAIL — `Hashing` no existe (error de compilación).

- [ ] **Step 3: Implementar `Hashing`**

Create `src/Resumenes.Core/Apoyos/Hashing.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Resumenes.Core.Apoyos;

public static class Hashing
{
    public static string Sha256HexDeTexto(string texto)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));

    public static string Sha256HexDeArchivo(string ruta)
    {
        using var stream = File.OpenRead(ruta);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    public static string ArchivoIdDesdeHash(string hashHex) => hashHex[..16];
}
```

- [ ] **Step 4: Correr el test y verificar que pasa**

Run: `dotnet test tests/Resumenes.Tests --filter HashingTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Core/Apoyos/Hashing.cs tests/Resumenes.Tests/HashingTests.cs
git commit -m "feat(core): utilidad Hashing SHA-256 con tests"
```

---

### Task 4: Utilidad `EscrituraAtomica` (TDD)

**Files:**
- Create: `src/Resumenes.Core/Apoyos/EscrituraAtomica.cs`, `tests/Resumenes.Tests/EscrituraAtomicaTests.cs`

**Interfaces:**
- Produces: `static class EscrituraAtomica` con `void Escribir(string ruta, string contenido)` (UTF-8 sin BOM) y `void EscribirBytes(string ruta, byte[] datos)`. Crea el directorio si falta; escribe a `<ruta>.tmp` y reemplaza atómicamente.

- [ ] **Step 1: Escribir el test que falla**

Create `tests/Resumenes.Tests/EscrituraAtomicaTests.cs`:

```csharp
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
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test tests/Resumenes.Tests --filter EscrituraAtomicaTests`
Expected: FAIL — `EscrituraAtomica` no existe.

- [ ] **Step 3: Implementar `EscrituraAtomica`**

Create `src/Resumenes.Core/Apoyos/EscrituraAtomica.cs`:

```csharp
using System.Text;

namespace Resumenes.Core.Apoyos;

public static class EscrituraAtomica
{
    private static readonly UTF8Encoding Utf8SinBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void Escribir(string ruta, string contenido)
        => EscribirBytes(ruta, Utf8SinBom.GetBytes(contenido));

    public static void EscribirBytes(string ruta, byte[] datos)
    {
        var dir = Path.GetDirectoryName(ruta);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = ruta + ".tmp";
        File.WriteAllBytes(tmp, datos);
        // File.Move con overwrite usa MoveFileEx (reemplazo atómico en el mismo volumen).
        File.Move(tmp, ruta, overwrite: true);
    }
}
```

- [ ] **Step 4: Correr el test y verificar que pasa**

Run: `dotnet test tests/Resumenes.Tests --filter EscrituraAtomicaTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Core/Apoyos/EscrituraAtomica.cs tests/Resumenes.Tests/EscrituraAtomicaTests.cs
git commit -m "feat(core): EscrituraAtomica con tests"
```

---

### Task 5: `RelojUtcSistema` y el orquestador (TDD)

**Files:**
- Create: `src/Resumenes.Core/Apoyos/RelojUtcSistema.cs`, `src/Resumenes.Core/Orquestacion/PasoPipeline.cs`, `src/Resumenes.Core/Orquestacion/PipelineOrquestador.cs`, `tests/Resumenes.Tests/Fakes/Fakes.cs`, `tests/Resumenes.Tests/PipelineOrquestadorTests.cs`

**Interfaces:**
- Consumes: `IRepositorioEstado`, `IRelojUtc`, `Unidad`, `Etapa`, `EstadoUnidad` (Tasks 1-2).
- Produces:
  - `class RelojUtcSistema : IRelojUtc`.
  - `record PasoPipeline(Etapa Etapa, string? ArchivoId, string? TemaId, string? RutaArtefacto, Func<CancellationToken, Task<string>> CalcularHashEntrada, Func<CancellationToken, Task> Ejecutar, string? PromptVersion = null, string? ModeloIa = null)`.
  - `record ResultadoEjecucion(int Ok, int Salteados, int Errores, IReadOnlyList<string> MensajesError)`.
  - `class PipelineOrquestador(IRepositorioEstado repo, IRelojUtc reloj)` con `Task<ResultadoEjecucion> EjecutarAsync(string analisisId, IReadOnlyList<PasoPipeline> pasos, CancellationToken ct)`.

- [ ] **Step 1: Crear `RelojUtcSistema` y los tipos del orquestador**

Create `src/Resumenes.Core/Apoyos/RelojUtcSistema.cs`:

```csharp
using Resumenes.Core.Interfaces;

namespace Resumenes.Core.Apoyos;

public class RelojUtcSistema : IRelojUtc
{
    public DateTime Ahora() => DateTime.UtcNow;
}
```

Create `src/Resumenes.Core/Orquestacion/PasoPipeline.cs`:

```csharp
using Resumenes.Core.Modelos;

namespace Resumenes.Core.Orquestacion;

public record PasoPipeline(
    Etapa Etapa,
    string? ArchivoId,
    string? TemaId,
    string? RutaArtefacto,
    Func<CancellationToken, Task<string>> CalcularHashEntrada,
    Func<CancellationToken, Task> Ejecutar,
    string? PromptVersion = null,
    string? ModeloIa = null);

public record ResultadoEjecucion(
    int Ok,
    int Salteados,
    int Errores,
    IReadOnlyList<string> MensajesError);
```

- [ ] **Step 2: Escribir los tests que fallan (idempotencia, reanudación, invalidación, fijado, error)**

Create `tests/Resumenes.Tests/Fakes/Fakes.cs`:

```csharp
using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;

namespace Resumenes.Tests.Fakes;

public class RelojFijo : IRelojUtc
{
    public DateTime Valor { get; set; } = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
    public DateTime Ahora() => Valor;
}

// Repositorio en memoria, suficiente para testear el orquestador.
public class RepositorioEnMemoria : IRepositorioEstado
{
    private readonly Dictionary<string, Unidad> _unidades = new();
    public readonly List<Unidad> Guardados = new();

    private static string Clave(string a, string? arc, string? t, Etapa e) => $"{a}|{arc}|{t}|{e}";

    public void InicializarEsquema() { }
    public Analisis? ObtenerAnalisisPorFingerprint(string fingerprint) => null;
    public void GuardarAnalisis(Analisis a) { }
    public Archivo? ObtenerArchivo(string id) => null;
    public void GuardarArchivo(Archivo a) { }
    public Tema? ObtenerTema(string id) => null;
    public void GuardarTema(Tema t) { }
    public void GuardarTemaArchivo(string temaId, string archivoId) { }

    public Unidad? ObtenerUnidad(string analisisId, string? archivoId, string? temaId, Etapa etapa)
        => _unidades.TryGetValue(Clave(analisisId, archivoId, temaId, etapa), out var u) ? u : null;

    public void GuardarUnidad(Unidad u)
    {
        _unidades[Clave(u.AnalisisId, u.ArchivoId, u.TemaId, u.Etapa)] = u;
        Guardados.Add(u);
    }
}
```

Create `tests/Resumenes.Tests/PipelineOrquestadorTests.cs`:

```csharp
using Resumenes.Core.Modelos;
using Resumenes.Core.Orquestacion;
using Resumenes.Tests.Fakes;
using Xunit;

namespace Resumenes.Tests;

public class PipelineOrquestadorTests
{
    private static PasoPipeline Paso(string hash, Action onEjecutar, Etapa etapa = Etapa.LimpiezaIA)
        => new(etapa, "arc1", null, "ruta.txt",
            _ => Task.FromResult(hash),
            _ => { onEjecutar(); return Task.CompletedTask; });

    [Fact]
    public async Task Ejecuta_pasoPendiente_yLoMarcaCompletado()
    {
        var repo = new RepositorioEnMemoria();
        var orq = new PipelineOrquestador(repo, new RelojFijo());
        int veces = 0;
        var r = await orq.EjecutarAsync("an1", new[] { Paso("h1", () => veces++) }, default);

        Assert.Equal(1, veces);
        Assert.Equal(1, r.Ok);
        var u = repo.ObtenerUnidad("an1", "arc1", null, Etapa.LimpiezaIA)!;
        Assert.Equal(EstadoUnidad.Completado, u.Estado);
        Assert.Equal("h1", u.HashEntrada);
    }

    [Fact]
    public async Task NoReprocesa_siCompletadoYHashIgual()
    {
        var repo = new RepositorioEnMemoria();
        var orq = new PipelineOrquestador(repo, new RelojFijo());
        int veces = 0;
        await orq.EjecutarAsync("an1", new[] { Paso("h1", () => veces++) }, default);
        var r2 = await orq.EjecutarAsync("an1", new[] { Paso("h1", () => veces++) }, default);

        Assert.Equal(1, veces);          // no se volvió a ejecutar
        Assert.Equal(1, r2.Salteados);
    }

    [Fact]
    public async Task Reprocesa_siHashCambia()
    {
        var repo = new RepositorioEnMemoria();
        var orq = new PipelineOrquestador(repo, new RelojFijo());
        int veces = 0;
        await orq.EjecutarAsync("an1", new[] { Paso("h1", () => veces++) }, default);
        await orq.EjecutarAsync("an1", new[] { Paso("h2", () => veces++) }, default);

        Assert.Equal(2, veces);          // cambió el hash => reprocesa
    }

    [Fact]
    public async Task NoReprocesa_siFijadoPorUsuario_aunqueCambieHash()
    {
        var repo = new RepositorioEnMemoria();
        var orq = new PipelineOrquestador(repo, new RelojFijo());
        int veces = 0;
        await orq.EjecutarAsync("an1", new[] { Paso("h1", () => veces++) }, default);
        repo.ObtenerUnidad("an1", "arc1", null, Etapa.LimpiezaIA)!.FijadoPorUsuario = true;
        await orq.EjecutarAsync("an1", new[] { Paso("h2", () => veces++) }, default);

        Assert.Equal(1, veces);          // fijado => no reprocesa
    }

    [Fact]
    public async Task PasoQueLanza_marcaError_yNoCrashea_yDetiene()
    {
        var repo = new RepositorioEnMemoria();
        var orq = new PipelineOrquestador(repo, new RelojFijo());
        int veces2 = 0;
        var pasos = new[]
        {
            new PasoPipeline(Etapa.LimpiezaIA, "arc1", null, "r.txt",
                _ => Task.FromResult("h1"),
                _ => throw new InvalidOperationException("boom")),
            new PasoPipeline(Etapa.ResumenFinal, "arc1", null, "r2.txt",
                _ => Task.FromResult("h2"),
                _ => { veces2++; return Task.CompletedTask; }),
        };

        var r = await orq.EjecutarAsync("an1", pasos, default);

        Assert.Equal(1, r.Errores);
        Assert.Equal(0, veces2);         // no continúa la cadena tras el error
        var u = repo.ObtenerUnidad("an1", "arc1", null, Etapa.LimpiezaIA)!;
        Assert.Equal(EstadoUnidad.Error, u.Estado);
        Assert.Contains("boom", u.ErrorMsg);
    }
}
```

- [ ] **Step 3: Correr los tests y verificar que fallan**

Run: `dotnet test tests/Resumenes.Tests --filter PipelineOrquestadorTests`
Expected: FAIL — `PipelineOrquestador` no existe.

- [ ] **Step 4: Implementar `PipelineOrquestador`**

Create `src/Resumenes.Core/Orquestacion/PipelineOrquestador.cs`:

```csharp
using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;

namespace Resumenes.Core.Orquestacion;

public class PipelineOrquestador(IRepositorioEstado repo, IRelojUtc reloj)
{
    public async Task<ResultadoEjecucion> EjecutarAsync(
        string analisisId, IReadOnlyList<PasoPipeline> pasos, CancellationToken ct)
    {
        int ok = 0, salteados = 0, errores = 0;
        var mensajes = new List<string>();

        foreach (var paso in pasos)
        {
            ct.ThrowIfCancellationRequested();
            var existente = repo.ObtenerUnidad(analisisId, paso.ArchivoId, paso.TemaId, paso.Etapa);
            var hash = await paso.CalcularHashEntrada(ct);

            // Reutilización: completado + hash igual, o fijado por el usuario.
            if (existente is { Estado: EstadoUnidad.Completado } &&
                (existente.HashEntrada == hash || existente.FijadoPorUsuario))
            {
                salteados++;
                continue;
            }

            var unidad = existente ?? new Unidad
            {
                AnalisisId = analisisId, ArchivoId = paso.ArchivoId, TemaId = paso.TemaId, Etapa = paso.Etapa
            };
            unidad.Estado = EstadoUnidad.EnProceso;
            unidad.ActualizadoEn = reloj.Ahora();
            repo.GuardarUnidad(unidad);

            try
            {
                await paso.Ejecutar(ct);
                unidad.Estado = EstadoUnidad.Completado;
                unidad.HashEntrada = hash;
                unidad.RutaArtefacto = paso.RutaArtefacto;
                unidad.PromptVersion = paso.PromptVersion;
                unidad.ModeloIa = paso.ModeloIa;
                unidad.ErrorMsg = null;
                unidad.ActualizadoEn = reloj.Ahora();
                repo.GuardarUnidad(unidad);
                ok++;
            }
            catch (Exception ex)
            {
                unidad.Estado = EstadoUnidad.Error;
                unidad.ErrorMsg = ex.Message;
                unidad.ActualizadoEn = reloj.Ahora();
                repo.GuardarUnidad(unidad);
                errores++;
                mensajes.Add($"{paso.Etapa}: {ex.Message}");
                break; // la cadena es dependiente: no seguir tras un error
            }
        }

        return new ResultadoEjecucion(ok, salteados, errores, mensajes);
    }
}
```

- [ ] **Step 5: Correr los tests y verificar que pasan**

Run: `dotnet test tests/Resumenes.Tests --filter PipelineOrquestadorTests`
Expected: PASS (5 tests).

- [ ] **Step 6: Commit**

```bash
git add src/Resumenes.Core/Apoyos/RelojUtcSistema.cs src/Resumenes.Core/Orquestacion tests/Resumenes.Tests/Fakes tests/Resumenes.Tests/PipelineOrquestadorTests.cs
git commit -m "feat(core): PipelineOrquestador (idempotencia, reanudacion, tolerancia a error) con tests"
```

---

### Task 6: `schema.sql` embebido + `SqliteRepositorioEstado` (TDD)

**Files:**
- Create: `src/Resumenes.Infrastructure/schema.sql` (copiar el `schema.sql` de la raíz), `src/Resumenes.Infrastructure/Persistencia/SqliteRepositorioEstado.cs`, `tests/Resumenes.Tests/SqliteRepositorioEstadoTests.cs`
- Modify: `src/Resumenes.Infrastructure/Resumenes.Infrastructure.csproj` (embeber el schema)

**Interfaces:**
- Consumes: `IRepositorioEstado`, entidades.
- Produces: `class SqliteRepositorioEstado(string cadenaConexion) : IRepositorioEstado`.

- [ ] **Step 1: Copiar `schema.sql` al proyecto y embeberlo**

Run:

```bash
cp schema.sql src/Resumenes.Infrastructure/schema.sql
```

Modify `src/Resumenes.Infrastructure/Resumenes.Infrastructure.csproj` — agregar dentro de `<Project>`:

```xml
  <ItemGroup>
    <EmbeddedResource Include="schema.sql" />
  </ItemGroup>
```

- [ ] **Step 2: Escribir el test que falla**

Create `tests/Resumenes.Tests/SqliteRepositorioEstadoTests.cs`:

```csharp
using Resumenes.Core.Modelos;
using Resumenes.Infrastructure.Persistencia;
using Xunit;

namespace Resumenes.Tests;

public class SqliteRepositorioEstadoTests : IDisposable
{
    private readonly string _ruta = Path.Combine(Path.GetTempPath(), $"resu_{Guid.NewGuid():N}.sqlite");
    private SqliteRepositorioEstado Crear()
    {
        var repo = new SqliteRepositorioEstado($"Data Source={_ruta}");
        repo.InicializarEsquema();
        return repo;
    }

    [Fact]
    public void GuardaYRecupera_analisisPorFingerprint()
    {
        var repo = Crear();
        var a = new Analisis("an1", "Materia", @"C:\mat", "fp123", EstadoAnalisis.EnProceso,
            DateTime.UtcNow, DateTime.UtcNow);
        repo.GuardarAnalisis(a);

        var leido = repo.ObtenerAnalisisPorFingerprint("fp123");
        Assert.NotNull(leido);
        Assert.Equal("an1", leido!.Id);
        Assert.Equal(EstadoAnalisis.EnProceso, leido.Estado);
    }

    [Fact]
    public void GuardarUnidad_esUpsert_porClaveNatural()
    {
        var repo = Crear();
        repo.GuardarAnalisis(new Analisis("an1", "M", "c", "fp", EstadoAnalisis.EnProceso, DateTime.UtcNow, DateTime.UtcNow));
        var u = new Unidad { AnalisisId = "an1", ArchivoId = "arc1", Etapa = Etapa.OcrBruto, Estado = EstadoUnidad.Pendiente, ActualizadoEn = DateTime.UtcNow };
        repo.GuardarUnidad(u);
        u.Estado = EstadoUnidad.Completado;
        u.HashEntrada = "h1";
        repo.GuardarUnidad(u);

        var leido = repo.ObtenerUnidad("an1", "arc1", null, Etapa.OcrBruto);
        Assert.NotNull(leido);
        Assert.Equal(EstadoUnidad.Completado, leido!.Estado);
        Assert.Equal("h1", leido.HashEntrada);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_ruta)) File.Delete(_ruta);
    }
}
```

- [ ] **Step 3: Correr el test y verificar que falla**

Run: `dotnet test tests/Resumenes.Tests --filter SqliteRepositorioEstadoTests`
Expected: FAIL — `SqliteRepositorioEstado` no existe.

- [ ] **Step 4: Implementar `SqliteRepositorioEstado`**

Create `src/Resumenes.Infrastructure/Persistencia/SqliteRepositorioEstado.cs`:

```csharp
using System.Reflection;
using Microsoft.Data.Sqlite;
using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;

namespace Resumenes.Infrastructure.Persistencia;

public class SqliteRepositorioEstado(string cadenaConexion) : IRepositorioEstado
{
    private SqliteConnection Abrir()
    {
        var con = new SqliteConnection(cadenaConexion);
        con.Open();
        using var pragma = con.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return con;
    }

    public void InicializarEsquema()
    {
        var asm = Assembly.GetExecutingAssembly();
        var nombre = asm.GetManifestResourceNames().Single(n => n.EndsWith("schema.sql"));
        using var stream = asm.GetManifestResourceStream(nombre)!;
        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd();
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public Analisis? ObtenerAnalisisPorFingerprint(string fingerprint)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"SELECT id, nombre, carpeta_origen, fingerprint, estado, creado_en, actualizado_en
                            FROM Analisis WHERE fingerprint = $fp LIMIT 1;";
        cmd.Parameters.AddWithValue("$fp", fingerprint);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Analisis(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3),
            Enum.Parse<EstadoAnalisis>(r.GetString(4)),
            DateTime.Parse(r.GetString(5)), DateTime.Parse(r.GetString(6)));
    }

    public void GuardarAnalisis(Analisis a)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"INSERT INTO Analisis (id, nombre, carpeta_origen, fingerprint, estado, creado_en, actualizado_en)
                            VALUES ($id,$n,$c,$fp,$e,$cr,$ac)
                            ON CONFLICT(id) DO UPDATE SET nombre=$n, carpeta_origen=$c, fingerprint=$fp,
                                estado=$e, actualizado_en=$ac;";
        cmd.Parameters.AddWithValue("$id", a.Id);
        cmd.Parameters.AddWithValue("$n", a.Nombre);
        cmd.Parameters.AddWithValue("$c", a.CarpetaOrigen);
        cmd.Parameters.AddWithValue("$fp", a.Fingerprint);
        cmd.Parameters.AddWithValue("$e", a.Estado.ToString());
        cmd.Parameters.AddWithValue("$cr", a.CreadoEn.ToString("o"));
        cmd.Parameters.AddWithValue("$ac", a.ActualizadoEn.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public Archivo? ObtenerArchivo(string id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"SELECT id, analisis_id, nombre_original, ruta_relativa, hash_sha256, tamano_bytes, tipo, paginas, creado_en
                            FROM Archivo WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Archivo(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
            r.GetInt64(5), Enum.Parse<TipoArchivo>(r.GetString(6)),
            r.IsDBNull(7) ? null : r.GetInt32(7), DateTime.Parse(r.GetString(8)));
    }

    public void GuardarArchivo(Archivo a)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"INSERT INTO Archivo (id, analisis_id, nombre_original, ruta_relativa, hash_sha256, tamano_bytes, tipo, paginas, creado_en)
                            VALUES ($id,$an,$no,$rr,$h,$t,$tipo,$pag,$cr)
                            ON CONFLICT(id) DO UPDATE SET ruta_relativa=$rr, paginas=$pag;";
        cmd.Parameters.AddWithValue("$id", a.Id);
        cmd.Parameters.AddWithValue("$an", a.AnalisisId);
        cmd.Parameters.AddWithValue("$no", a.NombreOriginal);
        cmd.Parameters.AddWithValue("$rr", a.RutaRelativa);
        cmd.Parameters.AddWithValue("$h", a.HashSha256);
        cmd.Parameters.AddWithValue("$t", a.TamanoBytes);
        cmd.Parameters.AddWithValue("$tipo", a.Tipo.ToString());
        cmd.Parameters.AddWithValue("$pag", (object?)a.Paginas ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cr", a.CreadoEn.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public Tema? ObtenerTema(string id)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT id, analisis_id, nombre, orden, confirmado_por_usuario FROM Tema WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Tema(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetInt32(4) != 0);
    }

    public void GuardarTema(Tema t)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"INSERT INTO Tema (id, analisis_id, nombre, orden, confirmado_por_usuario)
                            VALUES ($id,$an,$n,$o,$c)
                            ON CONFLICT(id) DO UPDATE SET nombre=$n, orden=$o, confirmado_por_usuario=$c;";
        cmd.Parameters.AddWithValue("$id", t.Id);
        cmd.Parameters.AddWithValue("$an", t.AnalisisId);
        cmd.Parameters.AddWithValue("$n", t.Nombre);
        cmd.Parameters.AddWithValue("$o", t.Orden);
        cmd.Parameters.AddWithValue("$c", t.ConfirmadoPorUsuario ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public void GuardarTemaArchivo(string temaId, string archivoId)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO TemaArchivo (tema_id, archivo_id) VALUES ($t,$a);";
        cmd.Parameters.AddWithValue("$t", temaId);
        cmd.Parameters.AddWithValue("$a", archivoId);
        cmd.ExecuteNonQuery();
    }

    public Unidad? ObtenerUnidad(string analisisId, string? archivoId, string? temaId, Etapa etapa)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"SELECT id, analisis_id, archivo_id, tema_id, etapa, estado, ruta_artefacto, hash_entrada,
                                   prompt_version, modelo_ia, tokens, fijado_por_usuario, error_msg, actualizado_en
                            FROM Unidad
                            WHERE analisis_id=$an AND COALESCE(archivo_id,'')=$arc
                              AND COALESCE(tema_id,'')=$t AND etapa=$e;";
        cmd.Parameters.AddWithValue("$an", analisisId);
        cmd.Parameters.AddWithValue("$arc", archivoId ?? "");
        cmd.Parameters.AddWithValue("$t", temaId ?? "");
        cmd.Parameters.AddWithValue("$e", etapa.ToString());
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Unidad
        {
            Id = r.GetInt64(0),
            AnalisisId = r.GetString(1),
            ArchivoId = r.IsDBNull(2) ? null : r.GetString(2),
            TemaId = r.IsDBNull(3) ? null : r.GetString(3),
            Etapa = Enum.Parse<Etapa>(r.GetString(4)),
            Estado = Enum.Parse<EstadoUnidad>(r.GetString(5)),
            RutaArtefacto = r.IsDBNull(6) ? null : r.GetString(6),
            HashEntrada = r.IsDBNull(7) ? null : r.GetString(7),
            PromptVersion = r.IsDBNull(8) ? null : r.GetString(8),
            ModeloIa = r.IsDBNull(9) ? null : r.GetString(9),
            Tokens = r.IsDBNull(10) ? null : r.GetInt32(10),
            FijadoPorUsuario = r.GetInt32(11) != 0,
            ErrorMsg = r.IsDBNull(12) ? null : r.GetString(12),
            ActualizadoEn = DateTime.Parse(r.GetString(13))
        };
    }

    public void GuardarUnidad(Unidad u)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"INSERT INTO Unidad (analisis_id, archivo_id, tema_id, etapa, estado, ruta_artefacto,
                                hash_entrada, prompt_version, modelo_ia, tokens, fijado_por_usuario, error_msg, actualizado_en)
                            VALUES ($an,$arc,$t,$e,$est,$ruta,$he,$pv,$mi,$tok,$fij,$err,$ac)
                            ON CONFLICT(analisis_id, COALESCE(archivo_id,''), COALESCE(tema_id,''), etapa)
                            DO UPDATE SET estado=$est, ruta_artefacto=$ruta, hash_entrada=$he, prompt_version=$pv,
                                modelo_ia=$mi, tokens=$tok, fijado_por_usuario=$fij, error_msg=$err, actualizado_en=$ac;";
        cmd.Parameters.AddWithValue("$an", u.AnalisisId);
        cmd.Parameters.AddWithValue("$arc", (object?)u.ArchivoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$t", (object?)u.TemaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$e", u.Etapa.ToString());
        cmd.Parameters.AddWithValue("$est", u.Estado.ToString());
        cmd.Parameters.AddWithValue("$ruta", (object?)u.RutaArtefacto ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$he", (object?)u.HashEntrada ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pv", (object?)u.PromptVersion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$mi", (object?)u.ModeloIa ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tok", (object?)u.Tokens ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$fij", u.FijadoPorUsuario ? 1 : 0);
        cmd.Parameters.AddWithValue("$err", (object?)u.ErrorMsg ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ac", u.ActualizadoEn.ToString("o"));
        cmd.ExecuteNonQuery();
    }
}
```

> Nota: el índice único de `Unidad` usa `COALESCE(archivo_id,''), COALESCE(tema_id,'')` (igual que en `schema.sql`), por eso el `ON CONFLICT` los repite.

- [ ] **Step 5: Correr el test y verificar que pasa**

Run: `dotnet test tests/Resumenes.Tests --filter SqliteRepositorioEstadoTests`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add src/Resumenes.Infrastructure/schema.sql src/Resumenes.Infrastructure/Resumenes.Infrastructure.csproj src/Resumenes.Infrastructure/Persistencia tests/Resumenes.Tests/SqliteRepositorioEstadoTests.cs
git commit -m "feat(infra): SqliteRepositorioEstado con schema embebido y tests"
```

---

### Task 7: `DeepseekClienteIA` (TDD con `HttpMessageHandler` falso)

**Files:**
- Create: `src/Resumenes.Infrastructure/IA/DeepseekClienteIA.cs`, `tests/Resumenes.Tests/DeepseekClienteIATests.cs`

**Interfaces:**
- Consumes: `IClienteIA`, `SolicitudIA`, `RespuestaIA`, `IAlmacenSecretos`.
- Produces: `class DeepseekClienteIA(HttpClient http, IAlmacenSecretos secretos, string baseUrl) : IClienteIA`. POST `{baseUrl}/chat/completions`; lee `choices[0].message.content`, `choices[0].finish_reason`, `usage.*`.

- [ ] **Step 1: Escribir el test que falla**

Create `tests/Resumenes.Tests/DeepseekClienteIATests.cs`:

```csharp
using System.Net;
using System.Text;
using Resumenes.Core.Interfaces;
using Resumenes.Infrastructure.IA;
using Xunit;

namespace Resumenes.Tests;

public class DeepseekClienteIATests
{
    private sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> fn) : HttpMessageHandler
    {
        public HttpRequestMessage? UltimaPeticion;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UltimaPeticion = request;
            if (request.Content != null) await request.Content.LoadIntoBufferAsync();
            return fn(request);
        }
    }

    private sealed class SecretosFijo(string? key) : IAlmacenSecretos
    {
        public void GuardarApiKey(string k) { }
        public string? ObtenerApiKey() => key;
    }

    [Fact]
    public async Task Completar_parseaContenidoUsageYFinishReason()
    {
        var json = """
        {"choices":[{"message":{"content":"texto limpio"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}
        """;
        var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var cliente = new DeepseekClienteIA(new HttpClient(handler), new SecretosFijo("k-123"), "https://api.deepseek.com");

        var r = await cliente.CompletarAsync(
            new SolicitudIA("sys", "user", 0.2, 1000, "limpieza-v1", "deepseek-chat"), default);

        Assert.Equal("texto limpio", r.Texto);
        Assert.Equal("stop", r.FinishReason);
        Assert.Equal(15, r.TokensTotal);
        Assert.Equal("Bearer k-123", handler.UltimaPeticion!.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task Completar_sinApiKey_lanzaInvalidOperation()
    {
        var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var cliente = new DeepseekClienteIA(new HttpClient(handler), new SecretosFijo(null), "https://api.deepseek.com");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cliente.CompletarAsync(new SolicitudIA("s", "u", 0.2, 100, "v1", "deepseek-chat"), default));
    }
}
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test tests/Resumenes.Tests --filter DeepseekClienteIATests`
Expected: FAIL — `DeepseekClienteIA` no existe.

- [ ] **Step 3: Implementar `DeepseekClienteIA`**

Create `src/Resumenes.Infrastructure/IA/DeepseekClienteIA.cs`:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.IA;

public class DeepseekClienteIA(HttpClient http, IAlmacenSecretos secretos, string baseUrl) : IClienteIA
{
    public async Task<RespuestaIA> CompletarAsync(SolicitudIA req, CancellationToken ct)
    {
        var key = secretos.ObtenerApiKey()
            ?? throw new InvalidOperationException("No hay API key de Deepseek configurada.");

        var cuerpo = new
        {
            model = req.Modelo,
            temperature = req.Temperatura,
            max_tokens = req.MaxTokens,
            messages = new object[]
            {
                new { role = "system", content = req.PromptSystem },
                new { role = "user", content = req.PromptUser }
            }
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = JsonContent.Create(cuerpo)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        using var resp = await http.SendAsync(msg, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var choice = root.GetProperty("choices")[0];
        var texto = choice.GetProperty("message").GetProperty("content").GetString() ?? "";
        var finish = choice.GetProperty("finish_reason").GetString() ?? "";
        var usage = root.GetProperty("usage");

        return new RespuestaIA(
            texto, finish,
            usage.GetProperty("prompt_tokens").GetInt32(),
            usage.GetProperty("completion_tokens").GetInt32(),
            usage.GetProperty("total_tokens").GetInt32());
    }
}
```

> Nota: los reintentos con Polly se configuran al construir el `HttpClient` en el Cli (Task 10). El cliente en sí queda simple y testeable.

- [ ] **Step 4: Correr el test y verificar que pasa**

Run: `dotnet test tests/Resumenes.Tests --filter DeepseekClienteIATests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Infrastructure/IA tests/Resumenes.Tests/DeepseekClienteIATests.cs
git commit -m "feat(infra): DeepseekClienteIA con tests (handler falso)"
```

---

### Task 8: Protocolo OCR NDJSON + adaptadores Python (rasterizar/OCR)

**Files:**
- Create: `src/Resumenes.Infrastructure/Ocr/ProtocoloOcr.cs`, `tests/Resumenes.Tests/ProtocoloOcrTests.cs`, `src/Resumenes.Infrastructure/Ocr/PyMuPdfRasterizador.cs`, `src/Resumenes.Infrastructure/Ocr/PaddleOcrServicio.cs`, `runtime/scripts/rasterizar.py`, `runtime/scripts/worker_ocr.py`

**Interfaces:**
- Consumes: `IRasterizador`, `IServicioOcr`.
- Produces:
  - `record MensajeOcr(string Tipo, string? ReqId, int? Pagina, string? Texto, string? Mensaje)` y `static MensajeOcr? ProtocoloOcr.Parsear(string lineaNdjson)`.
  - `class PyMuPdfRasterizador(string pythonExe, string scriptPath) : IRasterizador`.
  - `class PaddleOcrServicio(string pythonExe, string scriptPath, string modelosDir) : IServicioOcr`.

- [ ] **Step 1: Escribir el test del parser NDJSON (falla)**

Create `tests/Resumenes.Tests/ProtocoloOcrTests.cs`:

```csharp
using Resumenes.Infrastructure.Ocr;
using Xunit;

namespace Resumenes.Tests;

public class ProtocoloOcrTests
{
    [Fact]
    public void Parsea_result()
    {
        var m = ProtocoloOcr.Parsear("""{"type":"result","req_id":"r1","pagina":2,"texto":"hola ñ"}""");
        Assert.NotNull(m);
        Assert.Equal("result", m!.Tipo);
        Assert.Equal("r1", m.ReqId);
        Assert.Equal(2, m.Pagina);
        Assert.Equal("hola ñ", m.Texto);
    }

    [Fact]
    public void Parsea_ready_yError()
    {
        Assert.Equal("ready", ProtocoloOcr.Parsear("""{"type":"ready"}""")!.Tipo);
        var e = ProtocoloOcr.Parsear("""{"type":"error","req_id":"r1","mensaje":"boom"}""");
        Assert.Equal("error", e!.Tipo);
        Assert.Equal("boom", e.Mensaje);
    }

    [Fact]
    public void LineaInvalida_devuelveNull()
    {
        Assert.Null(ProtocoloOcr.Parsear("no es json"));
        Assert.Null(ProtocoloOcr.Parsear(""));
    }
}
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test tests/Resumenes.Tests --filter ProtocoloOcrTests`
Expected: FAIL — `ProtocoloOcr` no existe.

- [ ] **Step 3: Implementar `ProtocoloOcr`**

Create `src/Resumenes.Infrastructure/Ocr/ProtocoloOcr.cs`:

```csharp
using System.Text.Json;

namespace Resumenes.Infrastructure.Ocr;

public record MensajeOcr(string Tipo, string? ReqId, int? Pagina, string? Texto, string? Mensaje);

public static class ProtocoloOcr
{
    public static MensajeOcr? Parsear(string lineaNdjson)
    {
        if (string.IsNullOrWhiteSpace(lineaNdjson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(lineaNdjson);
            var e = doc.RootElement;
            if (!e.TryGetProperty("type", out var tipoEl)) return null;
            return new MensajeOcr(
                tipoEl.GetString() ?? "",
                e.TryGetProperty("req_id", out var r) ? r.GetString() : null,
                e.TryGetProperty("pagina", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null,
                e.TryGetProperty("texto", out var t) ? t.GetString() : null,
                e.TryGetProperty("mensaje", out var m) ? m.GetString() : null);
        }
        catch (JsonException) { return null; }
    }
}
```

- [ ] **Step 4: Correr el test y verificar que pasa**

Run: `dotnet test tests/Resumenes.Tests --filter ProtocoloOcrTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Crear el script Python de rasterización**

Create `runtime/scripts/rasterizar.py`:

```python
#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Rasteriza un PDF a JPG por pagina. Uso: rasterizar.py <pdf> <out_dir> <dpi>
Imprime una ruta de imagen por linea (stdout)."""
import sys, os
import fitz  # PyMuPDF

def main():
    pdf_path, out_dir, dpi = sys.argv[1], sys.argv[2], int(sys.argv[3])
    os.makedirs(out_dir, exist_ok=True)
    doc = fitz.open(pdf_path)
    for i, page in enumerate(doc, start=1):
        pix = page.get_pixmap(dpi=dpi, colorspace=fitz.csGRAY)
        ruta = os.path.join(out_dir, f"pagina_{i:04d}.jpg")
        pix.save(ruta)
        print(ruta, flush=True)

if __name__ == "__main__":
    main()
```

- [ ] **Step 6: Crear el worker Python de OCR (NDJSON, larga vida)**

Create `runtime/scripts/worker_ocr.py`:

```python
#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Worker OCR de larga vida (PaddleOCR). Protocolo NDJSON por stdin/stdout.
Uso: worker_ocr.py <modelos_dir>. Lee {"req_id","ruta_imagen"} por linea."""
import sys, json
from paddleocr import PaddleOCR

def emit(obj):
    sys.stdout.write(json.dumps(obj, ensure_ascii=False) + "\n")
    sys.stdout.flush()

def main():
    # Modelos locales para funcionar offline.
    ocr = PaddleOCR(use_angle_cls=True, lang="es", use_gpu=False, show_log=False)
    emit({"type": "ready"})
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            req = json.loads(line)
            rid, ruta = req["req_id"], req["ruta_imagen"]
        except Exception as ex:
            emit({"type": "error", "req_id": None, "mensaje": f"peticion invalida: {ex}"})
            continue
        try:
            resultado = ocr.ocr(ruta, cls=True)
            lineas = []
            for bloque in (resultado or []):
                for item in (bloque or []):
                    lineas.append(item[1][0])
            emit({"type": "result", "req_id": rid, "texto": "\n".join(lineas)})
        except Exception as ex:
            emit({"type": "error", "req_id": rid, "mensaje": str(ex)})

if __name__ == "__main__":
    main()
```

- [ ] **Step 7: Implementar `PyMuPdfRasterizador`**

Create `src/Resumenes.Infrastructure/Ocr/PyMuPdfRasterizador.cs`:

```csharp
using System.Diagnostics;
using System.Text;
using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.Ocr;

public class PyMuPdfRasterizador(string pythonExe, string scriptPath) : IRasterizador
{
    public async Task<IReadOnlyList<string>> RasterizarAsync(string pdfPath, string outDir, int dpi, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(pythonExe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add(pdfPath);
        psi.ArgumentList.Add(outDir);
        psi.ArgumentList.Add(dpi.ToString());

        using var proc = Process.Start(psi)!;
        var salida = await proc.StandardOutput.ReadToEndAsync(ct);
        var err = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"Rasterizado falló (exit {proc.ExitCode}): {err}");

        return salida.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
```

- [ ] **Step 8: Implementar `PaddleOcrServicio` (worker de larga vida)**

Create `src/Resumenes.Infrastructure/Ocr/PaddleOcrServicio.cs`:

```csharp
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.Ocr;

public class PaddleOcrServicio(string pythonExe, string scriptPath, string modelosDir) : IServicioOcr
{
    public async Task<string> OcrAsync(IReadOnlyList<string> rutasImagenes, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(pythonExe)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardInputEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add(modelosDir);

        using var proc = Process.Start(psi)!;
        try
        {
            // Esperar el "ready" del worker.
            string? linea;
            while ((linea = await proc.StandardOutput.ReadLineAsync(ct)) != null)
            {
                var m = ProtocoloOcr.Parsear(linea);
                if (m?.Tipo == "ready") break;
            }

            var textos = new List<string>();
            for (int i = 0; i < rutasImagenes.Count; i++)
            {
                var reqId = $"p{i + 1}";
                var pedido = JsonSerializer.Serialize(new { req_id = reqId, ruta_imagen = rutasImagenes[i] });
                await proc.StandardInput.WriteLineAsync(pedido);
                await proc.StandardInput.FlushAsync(ct);

                while ((linea = await proc.StandardOutput.ReadLineAsync(ct)) != null)
                {
                    var m = ProtocoloOcr.Parsear(linea);
                    if (m == null) continue;
                    if (m.Tipo == "result" && m.ReqId == reqId) { textos.Add(m.Texto ?? ""); break; }
                    if (m.Tipo == "error" && m.ReqId == reqId)
                        throw new InvalidOperationException($"OCR falló en {rutasImagenes[i]}: {m.Mensaje}");
                }
            }

            proc.StandardInput.Close();
            await proc.WaitForExitAsync(ct);
            return string.Join("\n\n", textos);
        }
        catch
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            throw;
        }
    }
}
```

- [ ] **Step 9: Compilar y correr los tests existentes**

Run: `dotnet test tests/Resumenes.Tests --filter ProtocoloOcrTests`
Expected: PASS. (Los adaptadores de proceso se verifican en integración manual, Task 11.)

- [ ] **Step 10: Commit**

```bash
git add src/Resumenes.Infrastructure/Ocr tests/Resumenes.Tests/ProtocoloOcrTests.cs runtime/scripts/rasterizar.py runtime/scripts/worker_ocr.py
git commit -m "feat(infra): adaptadores OCR (rasterizado + worker PaddleOCR NDJSON) y parser con tests"
```

---

### Task 9: `PythonGeneradorPdf` + `DpapiAlmacenSecretos`

**Files:**
- Create: `src/Resumenes.Infrastructure/Pdf/PythonGeneradorPdf.cs`, `src/Resumenes.Infrastructure/Secretos/DpapiAlmacenSecretos.cs`
- Copy: `generador_estudio_final.py` → `runtime/scripts/generador_estudio_final.py`

**Interfaces:**
- Consumes: `IGeneradorPdf`, `IAlmacenSecretos`.
- Produces: `class PythonGeneradorPdf(string pythonExe, string scriptPath) : IGeneradorPdf`; `class DpapiAlmacenSecretos(string rutaArchivo) : IAlmacenSecretos`.

- [ ] **Step 1: Copiar el generador de PDF al runtime**

Run:

```bash
cp generador_estudio_final.py runtime/scripts/generador_estudio_final.py
```

- [ ] **Step 2: Implementar `PythonGeneradorPdf`**

Create `src/Resumenes.Infrastructure/Pdf/PythonGeneradorPdf.cs`:

```csharp
using System.Diagnostics;
using System.Text;
using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.Pdf;

public class PythonGeneradorPdf(string pythonExe, string scriptPath) : IGeneradorPdf
{
    public async Task GenerarAsync(string contenidoPath, string pdfPath, string titulo, string subtitulo, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(pythonExe)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add(contenidoPath);
        psi.ArgumentList.Add(pdfPath);
        psi.ArgumentList.Add("--titulo"); psi.ArgumentList.Add(titulo);
        psi.ArgumentList.Add("--subtitulo"); psi.ArgumentList.Add(subtitulo);

        using var proc = Process.Start(psi)!;
        var err = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"Generación de PDF falló (exit {proc.ExitCode}): {err}");
        if (!File.Exists(pdfPath))
            throw new InvalidOperationException($"El script no produjo el PDF esperado: {pdfPath}");
    }
}
```

- [ ] **Step 3: Implementar `DpapiAlmacenSecretos`**

Create `src/Resumenes.Infrastructure/Secretos/DpapiAlmacenSecretos.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.Secretos;

// Cifra la API key con DPAPI (scope CurrentUser). Solo funciona en Windows.
public class DpapiAlmacenSecretos(string rutaArchivo) : IAlmacenSecretos
{
    public void GuardarApiKey(string key)
    {
        var cifrado = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser);
        var dir = Path.GetDirectoryName(rutaArchivo);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(rutaArchivo, cifrado);
    }

    public string? ObtenerApiKey()
    {
        if (!File.Exists(rutaArchivo)) return null;
        var cifrado = File.ReadAllBytes(rutaArchivo);
        var datos = ProtectedData.Unprotect(cifrado, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(datos);
    }
}
```

- [ ] **Step 4: Compilar**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Infrastructure/Pdf src/Resumenes.Infrastructure/Secretos runtime/scripts/generador_estudio_final.py
git commit -m "feat(infra): generador PDF (Python) y almacen de secretos DPAPI"
```

---

### Task 10: Composition root — `Resumenes.Cli`

**Files:**
- Create: `src/Resumenes.Cli/Configuracion.cs`, `src/Resumenes.Cli/ConstructorPipeline.cs`, `src/Resumenes.Cli/Program.cs`
- Create: `config/settings.ejemplo.json` (plantilla)

**Interfaces:**
- Consumes: TODO lo anterior.
- Produces: ejecutable de consola. Wiring de los pasos del pipeline para 1 archivo.

- [ ] **Step 1: Crear el modelo de configuración**

Create `src/Resumenes.Cli/Configuracion.cs`:

```csharp
namespace Resumenes.Cli;

public class Configuracion
{
    public string RutaWorkspace { get; set; } = "";
    public string PythonExe { get; set; } = "python";
    public string ScriptsDir { get; set; } = "runtime/scripts";
    public string ModelosPaddle { get; set; } = "runtime/modelos";
    public string FontsDir { get; set; } = "runtime/fonts";
    public int Dpi { get; set; } = 200;
    public string Modelo { get; set; } = "deepseek-chat";
    public string BaseUrlDeepseek { get; set; } = "https://api.deepseek.com";
}
```

Create `config/settings.ejemplo.json`:

```json
{
  "RutaWorkspace": "%LOCALAPPDATA%/ResumenesApp",
  "PythonExe": "python",
  "ScriptsDir": "runtime/scripts",
  "ModelosPaddle": "runtime/modelos",
  "FontsDir": "runtime/fonts",
  "Dpi": 200,
  "Modelo": "deepseek-chat",
  "BaseUrlDeepseek": "https://api.deepseek.com"
}
```

- [ ] **Step 2: Crear el constructor de pasos del pipeline**

Create `src/Resumenes.Cli/ConstructorPipeline.cs`:

```csharp
using Resumenes.Core.Apoyos;
using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;
using Resumenes.Core.Orquestacion;

namespace Resumenes.Cli;

// Arma los 6 pasos para 1 archivo -> 1 tema (Fase 1).
public class ConstructorPipeline(
    IRasterizador rasterizador, IServicioOcr ocr, IClienteIA ia,
    IGeneradorPdf pdf, IRepositorioEstado repo, Configuracion cfg)
{
    public IReadOnlyList<PasoPipeline> Construir(Analisis an, Archivo arc, string rutaArchivoOriginal)
    {
        var baseFuentes = Path.Combine(cfg.RutaWorkspace, "analisis", an.Id, "00_fuentes", arc.Id);
        var imagenesDir = Path.Combine(baseFuentes, "imagenes");
        var bruto = Path.Combine(baseFuentes, "texto_bruto", "bruto.txt");
        var limpio = Path.Combine(baseFuentes, "texto_limpio", "limpio.txt");

        var final = Path.Combine(cfg.RutaWorkspace, "analisis", an.Id, "id_AnalisisFinal");
        var temaId = "tema-01";
        var consolidado = Path.Combine(final, "consolidado", $"{temaId}.txt");
        var resumen = Path.Combine(final, "resumen", $"{temaId}.txt");
        var pdfSalida = Path.Combine(final, "final", "analisisfinal1.pdf");

        IReadOnlyList<string> imagenes = Array.Empty<string>();

        return new[]
        {
            new PasoPipeline(Etapa.Captura, arc.Id, null, imagenesDir,
                _ => Task.FromResult(arc.HashSha256),
                async ct => imagenes = await rasterizador.RasterizarAsync(rutaArchivoOriginal, imagenesDir, cfg.Dpi, ct)),

            new PasoPipeline(Etapa.OcrBruto, arc.Id, null, bruto,
                _ => Task.FromResult(arc.HashSha256),
                async ct =>
                {
                    if (imagenes.Count == 0)
                        imagenes = Directory.GetFiles(imagenesDir, "pagina_*.jpg").OrderBy(x => x).ToArray();
                    var texto = await ocr.OcrAsync(imagenes, ct);
                    EscrituraAtomica.Escribir(bruto, texto);
                }),

            new PasoPipeline(Etapa.LimpiezaIA, arc.Id, null, limpio,
                _ => Task.FromResult(Hashing.Sha256HexDeArchivo(bruto)),
                async ct =>
                {
                    var entrada = await File.ReadAllTextAsync(bruto, ct);
                    var r = await ia.CompletarAsync(new SolicitudIA(
                        Prompts.LimpiezaSystem, entrada, 0.2, 4000, "limpieza-v1", cfg.Modelo), ct);
                    EscrituraAtomica.Escribir(limpio, r.Texto);
                }, PromptVersion: "limpieza-v1", ModeloIa: cfg.Modelo),

            new PasoPipeline(Etapa.ConsolidacionTemas, null, temaId, consolidado,
                _ => Task.FromResult(Hashing.Sha256HexDeArchivo(limpio)),
                async ct =>
                {
                    repo.GuardarTema(new Tema(temaId, an.Id, arc.NombreOriginal, 1, true));
                    repo.GuardarTemaArchivo(temaId, arc.Id);
                    EscrituraAtomica.Escribir(consolidado, await File.ReadAllTextAsync(limpio, ct));
                }),

            new PasoPipeline(Etapa.ResumenFinal, null, temaId, resumen,
                _ => Task.FromResult(Hashing.Sha256HexDeArchivo(consolidado)),
                async ct =>
                {
                    var entrada = await File.ReadAllTextAsync(consolidado, ct);
                    var r = await ia.CompletarAsync(new SolicitudIA(
                        Prompts.ResumenSystem, entrada, 0.5, 4000, "resumen-v1", cfg.Modelo), ct);
                    EscrituraAtomica.Escribir(resumen, r.Texto);
                }, PromptVersion: "resumen-v1", ModeloIa: cfg.Modelo),

            new PasoPipeline(Etapa.GeneracionPDF, null, temaId, pdfSalida,
                _ => Task.FromResult(Hashing.Sha256HexDeArchivo(resumen)),
                async ct =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(pdfSalida)!);
                    await pdf.GenerarAsync(resumen, pdfSalida, arc.NombreOriginal, "Guía de estudio", ct);
                }),
        };
    }
}

public static class Prompts
{
    public const string LimpiezaSystem =
        "Sos un corrector de texto OCR en español. Corregí errores de OCR, reconstruí palabras " +
        "partidas y quitá ruido. PROHIBIDO agregar información que no esté en el texto. Respetá tildes y ñ. " +
        "Devolvé solo el texto corregido.";

    public const string ResumenSystem =
        "Sos un asistente de estudio. Resumí sin extremo, sin eliminar contenido, priorizando el original. " +
        "Devolvé el resultado en este formato de marcadores (uno por línea): " +
        "#TITULO:, #SUBTITULO:, y bloques @seccion:, @texto:, @blt:, @ejemplo:, @dato:, @tip:. " +
        "Usá \\n para saltos de línea dentro de un bloque. Lo que agregues de contexto marcalo con @dato o @tip.";
}
```

- [ ] **Step 3: Crear `Program.cs` (composition root)**

Create `src/Resumenes.Cli/Program.cs`:

```csharp
using System.Text;
using Polly;
using Polly.Retry;
using Resumenes.Cli;
using Resumenes.Core.Apoyos;
using Resumenes.Core.Modelos;
using Resumenes.Core.Orquestacion;
using Resumenes.Infrastructure.IA;
using Resumenes.Infrastructure.Ocr;
using Resumenes.Infrastructure.Pdf;
using Resumenes.Infrastructure.Persistencia;
using Resumenes.Infrastructure.Secretos;
using Serilog;
using System.Text.Json;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length < 1)
{
    Console.WriteLine("Uso: Resumenes.Cli <carpeta-con-1-pdf> [--set-key <APIKEY>]");
    return 1;
}

// Configuración
var raizApp = AppContext.BaseDirectory;
var cfgPath = Path.Combine(raizApp, "config", "settings.json");
var cfg = File.Exists(cfgPath)
    ? JsonSerializer.Deserialize<Configuracion>(File.ReadAllText(cfgPath))!
    : new Configuracion();
if (string.IsNullOrWhiteSpace(cfg.RutaWorkspace))
    cfg.RutaWorkspace = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ResumenesApp");
string Resolver(string r) => Path.IsPathRooted(r) ? r : Path.Combine(raizApp, r);

Directory.CreateDirectory(cfg.RutaWorkspace);
var configDir = Path.Combine(cfg.RutaWorkspace, "config");

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(cfg.RutaWorkspace, "logs", "app.log"), rollingInterval: RollingInterval.Day)
    .CreateLogger();

var secretos = new DpapiAlmacenSecretos(Path.Combine(configDir, "deepseek.key"));

// Permite cargar la API key: --set-key <valor>
var idxKey = Array.IndexOf(args, "--set-key");
if (idxKey >= 0 && idxKey + 1 < args.Length)
{
    secretos.GuardarApiKey(args[idxKey + 1]);
    Log.Information("API key guardada (cifrada con DPAPI).");
    return 0;
}

var carpeta = args[0];
var pdfs = Directory.GetFiles(carpeta, "*.pdf");
if (pdfs.Length == 0) { Log.Error("No hay PDF en {Carpeta}", carpeta); return 1; }
var rutaPdf = pdfs[0];

// Pre-vuelo de entorno
if (secretos.ObtenerApiKey() is null)
{
    Log.Error("No hay API key configurada. Cargala con: Resumenes.Cli --set-key <APIKEY>");
    return 1;
}

// Adaptadores
var pipelineHttp = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
    {
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestModuleException>().Handle<TaskCanceledException>()
            .HandleResult(r => (int)r.StatusCode == 429 || (int)r.StatusCode >= 500),
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential
    }).Build();
var http = new HttpClient(new PollyHandler(pipelineHttp)) { Timeout = TimeSpan.FromSeconds(120) };

var ia = new DeepseekClienteIA(http, secretos, cfg.BaseUrlDeepseek);
var rasterizador = new PyMuPdfRasterizador(cfg.PythonExe, Path.Combine(Resolver(cfg.ScriptsDir), "rasterizar.py"));
var ocr = new PaddleOcrServicio(cfg.PythonExe, Path.Combine(Resolver(cfg.ScriptsDir), "worker_ocr.py"), Resolver(cfg.ModelosPaddle));
var generadorPdf = new PythonGeneradorPdf(cfg.PythonExe, Path.Combine(Resolver(cfg.ScriptsDir), "generador_estudio_final.py"));

var repo = new SqliteRepositorioEstado($"Data Source={Path.Combine(cfg.RutaWorkspace, "data.sqlite")}");
repo.InicializarEsquema();
var reloj = new RelojUtcSistema();

// Registrar análisis + archivo
var hash = Hashing.Sha256HexDeArchivo(rutaPdf);
var fingerprint = Hashing.Sha256HexDeTexto($"{Path.GetFullPath(carpeta)}|{hash}");
var an = repo.ObtenerAnalisisPorFingerprint(fingerprint)
    ?? new Analisis(Guid.NewGuid().ToString("N"), Path.GetFileName(carpeta.TrimEnd('\\', '/')),
        Path.GetFullPath(carpeta), fingerprint, EstadoAnalisis.EnProceso, reloj.Ahora(), reloj.Ahora());
repo.GuardarAnalisis(an);

var arc = new Archivo(Hashing.ArchivoIdDesdeHash(hash), an.Id, Path.GetFileName(rutaPdf),
    Path.GetFileName(rutaPdf), hash, new FileInfo(rutaPdf).Length, TipoArchivo.Pdf, null, reloj.Ahora());
repo.GuardarArchivo(arc);

// Pipeline
var pasos = new ConstructorPipeline(rasterizador, ocr, ia, generadorPdf, repo, cfg).Construir(an, arc, rutaPdf);
var orq = new PipelineOrquestador(repo, reloj);
var resultado = await orq.EjecutarAsync(an.Id, pasos, CancellationToken.None);

Log.Information("Resultado: {Ok} ok / {Errores} error / {Salteados} salteados", resultado.Ok, resultado.Errores, resultado.Salteados);
foreach (var m in resultado.MensajesError) Log.Error("  - {Msg}", m);
var rutaFinal = Path.Combine(cfg.RutaWorkspace, "analisis", an.Id, "id_AnalisisFinal", "final", "analisisfinal1.pdf");
if (File.Exists(rutaFinal)) Log.Information("PDF: {Ruta}", rutaFinal);
Log.CloseAndFlush();
return resultado.Errores == 0 ? 0 : 2;

// Handler que aplica la pipeline de Polly al HttpClient.
sealed class PollyHandler(ResiliencePipeline<HttpResponseMessage> pipeline) : DelegatingHandler(new HttpClientHandler())
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => pipeline.ExecuteAsync(async token => await base.SendAsync(request, token), ct).AsTask();
}
```

> Nota sobre Polly: `HttpRequestModuleException` no existe — usar `HttpRequestException`. Corregir el `Handle<HttpRequestModuleException>()` por `Handle<HttpRequestException>()` al escribir el archivo. (Recordatorio incluido para evitar el typo.)

- [ ] **Step 4: Compilar la solución completa**

Run: `dotnet build`
Expected: `Build succeeded`. Si hay error por `HttpRequestModuleException`, reemplazar por `HttpRequestException`.

- [ ] **Step 5: Correr toda la suite de tests unitarios**

Run: `dotnet test`
Expected: PASS (todos los tests de Tasks 3-8).

- [ ] **Step 6: Commit**

```bash
git add src/Resumenes.Cli config/settings.ejemplo.json
git commit -m "feat(cli): composition root y wiring del pipeline de Fase 1"
```

---

### Task 11: Verificación de integración end-to-end (manual) + smoke test Python

**Files:**
- Create: `tests/python/test_generador_smoke.py`
- Modify: `docs/superpowers/plans/` (marcar criterios de aceptación)

**Interfaces:**
- Consumes: todo lo construido.
- Produces: evidencia de que los 5 criterios de aceptación se cumplen.

- [ ] **Step 1: Smoke test del generador de PDF (pytest)**

Create `tests/python/test_generador_smoke.py`:

```python
import os, subprocess, sys

def test_genera_pdf_no_vacio(tmp_path):
    raiz = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    script = os.path.join(raiz, "runtime", "scripts", "generador_estudio_final.py")
    entrada = os.path.join(raiz, "ejemplo_contenido.txt")
    salida = str(tmp_path / "salida.pdf")
    # Requiere fpdf2 y fuentes DejaVu en runtime/fonts o C:/Windows/Fonts.
    r = subprocess.run([sys.executable, script, entrada, salida], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    assert os.path.exists(salida) and os.path.getsize(salida) > 1000
```

- [ ] **Step 2: Correr el smoke test de Python**

Run: `python -m pytest tests/python/test_generador_smoke.py -v`
Expected: PASS. Si falla por fuentes, copiar `DejaVuSans*.ttf` a `runtime/fonts/`. Si falla por `fpdf2`, `pip install fpdf2`.

- [ ] **Step 3: Preparar entorno y fixture**

Verificar: `pip install fpdf2 paddleocr pymupdf` instalados; `DejaVuSans*.ttf` en `runtime/fonts/`; un PDF chico con tildes/ñ en una carpeta de prueba (p. ej. `tests/fixtures/prueba/con_tildes.pdf`). Cargar la API key:

Run: `dotnet run --project src/Resumenes.Cli -- --set-key TU_API_KEY_DEEPSEEK`
Expected: "API key guardada (cifrada con DPAPI)."

- [ ] **Step 4: Criterio 1 — run end-to-end produce PDF**

Run: `dotnet run --project src/Resumenes.Cli -- tests/fixtures/prueba`
Expected: log "Resultado: 6 ok / 0 error / 0 salteados" y una línea "PDF: ...analisisfinal1.pdf". Abrir el PDF y verificar **tildes/ñ correctas** (Criterio 4).

- [ ] **Step 5: Criterio 2 — re-ejecutar no reprocesa**

Run: `dotnet run --project src/Resumenes.Cli -- tests/fixtures/prueba`
Expected: log "0 ok / 0 error / 6 salteados" (no se gastan tokens de IA).

- [ ] **Step 6: Registrar evidencia**

Anotar en el log/PR los resultados de los pasos 4-5 (criterios 1, 2, 4 verificados; criterios 3 y 5 cubiertos por los tests unitarios del orquestador y por el cierre del worker en `PaddleOcrServicio`).

- [ ] **Step 7: Commit**

```bash
git add tests/python/test_generador_smoke.py
git commit -m "test: smoke test del generador PDF y verificacion end-to-end de Fase 1"
```

---

## Self-Review

**1. Spec coverage** (contra `2026-06-17-fase1-esqueleto-design.md`):
- Estructura de 4 proyectos → Task 0. Modelos/interfaces → Tasks 1-2. Hashing/EscrituraAtomica → Tasks 3-4. Orquestador (idempotencia/reanudación/invalidación/fijado/tolerancia) → Task 5. SQLite+schema → Task 6. Deepseek (IClienteIA) → Task 7. OCR (rasterizar+worker NDJSON) → Task 8. PDF + DPAPI → Task 9. Composition root + pre-vuelo + Polly + pipeline de 6 etapas con consolidación trivial → Task 10. Criterios de aceptación 1-5 + smoke test → Task 11. **Sin gaps.**

**2. Placeholder scan:** sin "TBD/TODO". Las dos notas (Polly `HttpRequestException`, índice `COALESCE`) son aclaraciones con la corrección explícita, no placeholders.

**3. Type consistency:** `PasoPipeline`/`ResultadoEjecucion` (Task 5) usados igual en Task 10. `SolicitudIA`/`RespuestaIA` (Task 2) consistentes en Tasks 7 y 10. `IRepositorioEstado` (Task 2) implementado en Task 6 y consumido en Tasks 5/10 con las mismas firmas. `Unidad` mutable usada igual en orquestador y repo. `MensajeOcr`/`ProtocoloOcr.Parsear` (Task 8) consistentes.

---

## Notas de implementación

- **`HttpRequestException`**, no `HttpRequestModuleException` (typo señalado en Task 10).
- El índice único de `Unidad` en `schema.sql` usa `COALESCE(archivo_id,''), COALESCE(tema_id,'')`; el `ON CONFLICT` del repo lo replica.
- Polly v8 (`ResiliencePipeline`): si la API difiere por versión, ajustar el `ResiliencePipelineBuilder` según el paquete instalado.
- Los modelos de PaddleOCR: en Fase 1 se permite que PaddleOCR los descargue la primera vez (requiere internet una vez); el empaquetado offline de modelos es de fases posteriores.
