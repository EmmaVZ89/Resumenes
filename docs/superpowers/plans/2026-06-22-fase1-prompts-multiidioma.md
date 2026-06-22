# Fase 1 — Prompts editables + multi-idioma neutro — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permitir editar desde Configuración la parte de rol/estilo de los 3 prompts (limpieza, detección, resumen) con "restaurar default", manteniendo el formato protegido, y dejar los prompts por defecto neutros (multi-idioma, sin forzar español).

**Architecture:** Se introduce `ServicioPrompts` (Infrastructure) que resuelve el prompt efectivo = `editable (override en SQLite o default) + parte fija`. `ConstructorPipeline` y `DetectorTemas` dejan de usar constantes y piden el prompt a `ServicioPrompts`. El `hash_entrada` de las unidades con IA incorpora el hash del prompt editable, de modo que editar un prompt invalida y reprocesa solo lo afectado. La UI agrega una sección "Prompts (avanzado)" en Configuración.

**Tech Stack:** .NET 9, C#, Microsoft.Data.Sqlite, WPF / WPF-UI 4.x, CommunityToolkit.Mvvm, xUnit.

## Global Constraints

- **Plataforma:** Windows-only; datos por usuario en `%LOCALAPPDATA%/ResumenesApp/`.
- **Puertos y adaptadores:** dominio + interfaces en `Resumenes.Core`; adaptadores en `Resumenes.Infrastructure`; UI (MVVM) en `Resumenes.Ui`.
- **SQLite es índice de estado**, no almacén de archivos. Solo la app .NET escribe la base. Timestamps ISO-8601 UTC, `TEXT`.
- **Idempotencia:** una `Unidad` por (analisis, archivo/tema, etapa); `hash_entrada` canónico decide si se reprocesa.
- **Formato del PDF protegido:** los marcadores `#TITULO:`, `#SUBTITULO:`, `@seccion:`, `@texto:`, `@blt:`, `@ejemplo:`, `@dato:`, `@tip:` NO son editables por el usuario.
- **Mínimos tokens:** no se agregan llamadas extra a la IA (la autodetección de idioma la hace el propio modelo en la misma llamada).
- **Idioma del LibreOffice/UI:** español; los prompts por defecto, neutros.
- **Tests:** lógica nueva con xUnit + `RepositorioEnMemoria`/fakes existentes. Las vistas XAML se validan manualmente (decisión del proyecto).
- **Build de verificación:** `dotnet build Resumenes.sln -c Debug` debe pasar (la solución incluye `Resumenes.Cli`).

---

## File Structure

- `src/Resumenes.Infrastructure/schema.sql` — **Modify**: agregar tabla `AjustePrompt`.
- `schema.sql` (copia en raíz) — **Modify**: misma tabla, para mantener sincronía documental.
- `src/Resumenes.Core/Interfaces/IRepositorioEstado.cs` — **Modify**: 3 métodos de `AjustePrompt`.
- `src/Resumenes.Infrastructure/Persistencia/SqliteRepositorioEstado.cs` — **Modify**: implementar esos métodos.
- `tests/Resumenes.Tests/Fakes/Fakes.cs` — **Modify**: `RepositorioEnMemoria` implementa los nuevos métodos.
- `src/Resumenes.Infrastructure/Aplicacion/ConstructorPipeline.cs` — **Modify**: clase `Prompts` pasa a exponer defaults neutros + partes fijas; `ConstructorPipeline` recibe `ServicioPrompts`; hash de limpieza/resumen incluye el prompt.
- `src/Resumenes.Infrastructure/Aplicacion/ServicioPrompts.cs` — **Create**: resolución/composición/hash de prompts.
- `src/Resumenes.Infrastructure/Aplicacion/DetectorTemas.cs` — **Modify**: recibe `ServicioPrompts` y lo usa.
- `src/Resumenes.Infrastructure/Aplicacion/ServicioAnalisis.cs` — **Modify**: construye `ServicioPrompts` y lo inyecta.
- `src/Resumenes.Ui/ViewModels/ConfiguracionVm.cs` — **Modify**: props + comandos de prompts.
- `src/Resumenes.Ui/App.xaml.cs` — **Modify**: registrar `ServicioPrompts` y factory de `ConfiguracionVm`.
- `src/Resumenes.Ui/Vistas/VistaConfiguracion.xaml` — **Modify**: sección "Prompts (avanzado)".
- `tests/Resumenes.Tests/ServicioPromptsTests.cs` — **Create**: tests de resolución/composición/hash/restaurar.

---

## Task 1: Tabla `AjustePrompt` y métodos de repositorio

**Files:**
- Modify: `src/Resumenes.Infrastructure/schema.sql`
- Modify: `schema.sql`
- Modify: `src/Resumenes.Core/Interfaces/IRepositorioEstado.cs`
- Modify: `src/Resumenes.Infrastructure/Persistencia/SqliteRepositorioEstado.cs`
- Modify: `tests/Resumenes.Tests/Fakes/Fakes.cs`
- Test: `tests/Resumenes.Tests/SqliteRepositorioEstadoTests.cs`

**Interfaces:**
- Produces:
  - `string? IRepositorioEstado.ObtenerAjustePrompt(string clave)`
  - `void IRepositorioEstado.GuardarAjustePrompt(string clave, string texto)`
  - `void IRepositorioEstado.EliminarAjustePrompt(string clave)`

- [ ] **Step 1: Escribir el test que falla**

En `tests/Resumenes.Tests/SqliteRepositorioEstadoTests.cs`, agregar al final de la clase (mirá los tests existentes para ver cómo crean el repo con archivo temporal e `InicializarEsquema()`):

```csharp
    [Fact]
    public void AjustePrompt_GuardarObtenerEliminar_Funciona()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"resu-{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteRepositorioEstado($"Data Source={tmp}");
            repo.InicializarEsquema();

            Assert.Null(repo.ObtenerAjustePrompt("resumen"));

            repo.GuardarAjustePrompt("resumen", "Texto del alumno");
            Assert.Equal("Texto del alumno", repo.ObtenerAjustePrompt("resumen"));

            // upsert: vuelve a guardar y pisa
            repo.GuardarAjustePrompt("resumen", "Otro texto");
            Assert.Equal("Otro texto", repo.ObtenerAjustePrompt("resumen"));

            repo.EliminarAjustePrompt("resumen");
            Assert.Null(repo.ObtenerAjustePrompt("resumen"));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test tests/Resumenes.Tests -c Debug --filter AjustePrompt_GuardarObtenerEliminar_Funciona`
Expected: FAIL de compilación — `IRepositorioEstado` no define `ObtenerAjustePrompt`.

- [ ] **Step 3: Agregar la tabla al esquema**

En `src/Resumenes.Infrastructure/schema.sql`, antes del bloque `-- META:` (la tabla `SchemaMeta`), insertar:

```sql
-- ----------------------------------------------------------------------------
-- AJUSTE_PROMPT: override editable por el usuario de la parte rol/estilo de un
-- prompt. Si no hay fila para una clave, se usa el default del código.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS AjustePrompt (
    clave          TEXT PRIMARY KEY,   -- 'limpieza' | 'deteccion' | 'resumen'
    texto_editable TEXT NOT NULL,
    actualizado_en TEXT NOT NULL
);
```

Y cambiar la versión del esquema (misma sección `SchemaMeta`):

```sql
INSERT OR IGNORE INTO SchemaMeta (clave, valor) VALUES ('schema_version', '2');
```

Copiar el mismo bloque `CREATE TABLE` y el cambio de versión a `schema.sql` de la raíz (es una copia documental; el que se embebe y ejecuta es el de `Resumenes.Infrastructure`).

- [ ] **Step 4: Declarar los métodos en la interfaz**

En `src/Resumenes.Core/Interfaces/IRepositorioEstado.cs`, agregar antes del cierre de la interfaz:

```csharp
    /// <summary>Texto editable (rol/estilo) override de un prompt; null si no hay override.</summary>
    string? ObtenerAjustePrompt(string clave);
    void GuardarAjustePrompt(string clave, string texto);
    /// <summary>Borra el override (vuelve al default del código).</summary>
    void EliminarAjustePrompt(string clave);
```

- [ ] **Step 5: Implementar en SqliteRepositorioEstado**

En `src/Resumenes.Infrastructure/Persistencia/SqliteRepositorioEstado.cs`, agregar antes del cierre de la clase:

```csharp
    public string? ObtenerAjustePrompt(string clave)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT texto_editable FROM AjustePrompt WHERE clave = $c;";
        cmd.Parameters.AddWithValue("$c", clave);
        var r = cmd.ExecuteScalar();
        return r is string s ? s : null;
    }

    public void GuardarAjustePrompt(string clave, string texto)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"INSERT INTO AjustePrompt (clave, texto_editable, actualizado_en)
                            VALUES ($c, $t, $a)
                            ON CONFLICT(clave) DO UPDATE SET texto_editable=$t, actualizado_en=$a;";
        cmd.Parameters.AddWithValue("$c", clave);
        cmd.Parameters.AddWithValue("$t", texto);
        cmd.Parameters.AddWithValue("$a", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void EliminarAjustePrompt(string clave)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM AjustePrompt WHERE clave = $c;";
        cmd.Parameters.AddWithValue("$c", clave);
        cmd.ExecuteNonQuery();
    }
```

- [ ] **Step 6: Actualizar el fake `RepositorioEnMemoria`**

En `tests/Resumenes.Tests/Fakes/Fakes.cs`, dentro de `RepositorioEnMemoria`, agregar un diccionario y los métodos (antes del cierre de la clase):

```csharp
    private readonly Dictionary<string, string> _ajustes = new();
    public string? ObtenerAjustePrompt(string clave) => _ajustes.TryGetValue(clave, out var v) ? v : null;
    public void GuardarAjustePrompt(string clave, string texto) => _ajustes[clave] = texto;
    public void EliminarAjustePrompt(string clave) => _ajustes.Remove(clave);
```

- [ ] **Step 7: Correr el test y verificar que pasa**

Run: `dotnet test tests/Resumenes.Tests -c Debug --filter AjustePrompt_GuardarObtenerEliminar_Funciona`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Resumenes.Infrastructure/schema.sql schema.sql \
        src/Resumenes.Core/Interfaces/IRepositorioEstado.cs \
        src/Resumenes.Infrastructure/Persistencia/SqliteRepositorioEstado.cs \
        tests/Resumenes.Tests/Fakes/Fakes.cs \
        tests/Resumenes.Tests/SqliteRepositorioEstadoTests.cs
git commit -m "feat(prompts): tabla AjustePrompt y metodos de repositorio"
```

---

## Task 2: Defaults neutros y `ServicioPrompts`

**Files:**
- Modify: `src/Resumenes.Infrastructure/Aplicacion/ConstructorPipeline.cs:164-196` (clase `Prompts`)
- Create: `src/Resumenes.Infrastructure/Aplicacion/ServicioPrompts.cs`
- Test: `tests/Resumenes.Tests/ServicioPromptsTests.cs`

**Interfaces:**
- Consumes: `IRepositorioEstado.ObtenerAjustePrompt/GuardarAjustePrompt/EliminarAjustePrompt` (Task 1); `Hashing.Sha256HexDeTexto(string)`.
- Produces (todos en `Resumenes.Infrastructure.Aplicacion.ServicioPrompts`):
  - consts: `ClaveLimpieza="limpieza"`, `ClaveDeteccion="deteccion"`, `ClaveResumen="resumen"`
  - `ServicioPrompts(IRepositorioEstado repo)`
  - `string DefaultEditable(string clave)`
  - `string TextoFijo(string clave)`
  - `string ObtenerEditable(string clave)`
  - `void GuardarEditable(string clave, string texto)`
  - `void RestaurarDefault(string clave)`
  - `string HashEditable(string clave)`
  - `string SystemLimpieza()`
  - `string SystemDeteccion(string promptTemas)`
  - `string SystemResumen(string nombreTema, string? promptAlumno)`
- Produces (en clase `Prompts`, ahora defaults neutros públicos):
  - `Prompts.LimpiezaEditableDefault`, `Prompts.LimpiezaFijo`
  - `Prompts.DeteccionEditableDefault`, `Prompts.DeteccionFijo`
  - `Prompts.ResumenEditableDefault`, `Prompts.ResumenFijo`

- [ ] **Step 1: Escribir los tests que fallan**

Crear `tests/Resumenes.Tests/ServicioPromptsTests.cs`:

```csharp
using Resumenes.Infrastructure.Aplicacion;
using Resumenes.Tests.Fakes;
using Xunit;

namespace Resumenes.Tests;

public class ServicioPromptsTests
{
    private static ServicioPrompts Nuevo() => new(new RepositorioEnMemoria());

    [Fact]
    public void Editable_SinOverride_DevuelveDefault()
    {
        var sp = Nuevo();
        Assert.Equal(Prompts.ResumenEditableDefault, sp.ObtenerEditable(ServicioPrompts.ClaveResumen));
    }

    [Fact]
    public void Editable_ConOverride_DevuelveOverride_yRestaurarVuelveAlDefault()
    {
        var sp = Nuevo();
        sp.GuardarEditable(ServicioPrompts.ClaveResumen, "Mi estilo");
        Assert.Equal("Mi estilo", sp.ObtenerEditable(ServicioPrompts.ClaveResumen));

        sp.RestaurarDefault(ServicioPrompts.ClaveResumen);
        Assert.Equal(Prompts.ResumenEditableDefault, sp.ObtenerEditable(ServicioPrompts.ClaveResumen));
    }

    [Fact]
    public void SystemResumen_IncluyeFormatoFijo_yNombreTema()
    {
        var sp = Nuevo();
        var s = sp.SystemResumen("Aduanas", null);
        Assert.Contains("#TITULO:", s);          // formato fijo siempre presente
        Assert.Contains("Aduanas", s);
    }

    [Fact]
    public void SystemResumen_ConPromptAlumno_PrioridadAlAlumno()
    {
        var sp = Nuevo();
        var s = sp.SystemResumen("Aduanas", "solo multiple choice");
        Assert.Contains("solo multiple choice", s);
        Assert.Contains("#TITULO:", s);          // formato sigue protegido
    }

    [Fact]
    public void SystemDeteccion_IncluyeFormatoJson()
    {
        var sp = Nuevo();
        var s = sp.SystemDeteccion("");
        Assert.Contains("\"temas\"", s);
    }

    [Fact]
    public void HashEditable_CambiaAlEditar()
    {
        var sp = Nuevo();
        var antes = sp.HashEditable(ServicioPrompts.ClaveLimpieza);
        sp.GuardarEditable(ServicioPrompts.ClaveLimpieza, "Otro corrector");
        var despues = sp.HashEditable(ServicioPrompts.ClaveLimpieza);
        Assert.NotEqual(antes, despues);
    }
}
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test tests/Resumenes.Tests -c Debug --filter ServicioPromptsTests`
Expected: FAIL de compilación (`ServicioPrompts` y los nuevos miembros de `Prompts` no existen).

- [ ] **Step 3: Reescribir la clase `Prompts` con defaults neutros**

En `src/Resumenes.Infrastructure/Aplicacion/ConstructorPipeline.cs`, reemplazar TODA la clase `public static class Prompts { ... }` (líneas 164-196) por:

```csharp
// Defaults NEUTROS (multi-idioma) y partes FIJAS de cada prompt. La parte fija nunca
// es editable por el usuario (sostiene el parseo del PDF / el JSON de detección).
public static class Prompts
{
    // ── Limpieza de OCR ──
    public const string LimpiezaEditableDefault =
        "Sos un corrector de texto OCR. Corregí errores de OCR, reconstruí palabras partidas " +
        "y quitá ruido. Mantené el idioma original del texto y respetá su ortografía, tildes y signos. " +
        "PROHIBIDO agregar información que no esté en el texto.";
    public const string LimpiezaFijo =
        "Devolvé solo el texto corregido.";

    // ── Detección de temas ──
    public const string DeteccionEditableDefault =
        "Sos un organizador de material de estudio. Agrupá el contenido de los archivos en TEMAS " +
        "coherentes para estudiar (ni demasiados ni demasiado pocos).";
    public const string DeteccionFijo =
        "Devolvé SOLO un JSON con la forma {\"temas\":[{\"nombre\":\"...\",\"archivos\":[\"<archivo_id>\"]}]} " +
        "usando exactamente los <archivo_id> que aparecen como '### ARCHIVO <id>'. Sin nada de texto fuera del JSON.";

    // ── Resumen ──
    public const string ResumenEditableDefault =
        "Sos un asistente de estudio. Resumí en el mismo idioma del material, sin extremos, " +
        "sin eliminar contenido, priorizando el original.";
    public const string ResumenFijo =
        "Devolvé el resultado en este formato de marcadores (uno por línea): " +
        "#TITULO:, #SUBTITULO:, y bloques @seccion:, @texto:, @blt:, @ejemplo:, @dato:, @tip:. " +
        "Usá \\n para saltos de línea dentro de un bloque. Lo que agregues de contexto marcalo con @dato o @tip.";
}
```

(El método `ResumenSystem` se elimina de aquí; su lógica pasa a `ServicioPrompts.SystemResumen` en este mismo paso. La referencia en el pipeline se corrige en Task 3.)

- [ ] **Step 4: Crear `ServicioPrompts`**

Crear `src/Resumenes.Infrastructure/Aplicacion/ServicioPrompts.cs`:

```csharp
using Resumenes.Core.Apoyos;
using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.Aplicacion;

/// <summary>
/// Resuelve el prompt efectivo de cada etapa con IA: parte EDITABLE (override del usuario en
/// SQLite, o el default neutro del código) + parte FIJA (formato protegido). Expone el hash del
/// texto editable para sumarlo al hash_entrada de las unidades (idempotencia: editar el prompt
/// invalida y reprocesa).
/// </summary>
public class ServicioPrompts(IRepositorioEstado repo)
{
    public const string ClaveLimpieza  = "limpieza";
    public const string ClaveDeteccion = "deteccion";
    public const string ClaveResumen   = "resumen";

    public string DefaultEditable(string clave) => clave switch
    {
        ClaveLimpieza  => Prompts.LimpiezaEditableDefault,
        ClaveDeteccion => Prompts.DeteccionEditableDefault,
        ClaveResumen   => Prompts.ResumenEditableDefault,
        _ => throw new ArgumentException($"Clave de prompt desconocida: {clave}")
    };

    public string TextoFijo(string clave) => clave switch
    {
        ClaveLimpieza  => Prompts.LimpiezaFijo,
        ClaveDeteccion => Prompts.DeteccionFijo,
        ClaveResumen   => Prompts.ResumenFijo,
        _ => throw new ArgumentException($"Clave de prompt desconocida: {clave}")
    };

    public string ObtenerEditable(string clave)
    {
        var ov = repo.ObtenerAjustePrompt(clave);
        return string.IsNullOrWhiteSpace(ov) ? DefaultEditable(clave) : ov;
    }

    public void GuardarEditable(string clave, string texto) => repo.GuardarAjustePrompt(clave, texto);

    public void RestaurarDefault(string clave) => repo.EliminarAjustePrompt(clave);

    public string HashEditable(string clave) => Hashing.Sha256HexDeTexto(ObtenerEditable(clave));

    public string SystemLimpieza() =>
        $"{ObtenerEditable(ClaveLimpieza)} {Prompts.LimpiezaFijo}";

    public string SystemDeteccion(string promptTemas) =>
        ObtenerEditable(ClaveDeteccion) + " " +
        (string.IsNullOrWhiteSpace(promptTemas) ? "" : $"Priorizá estos temas indicados por el alumno: {promptTemas}. ") +
        Prompts.DeteccionFijo;

    public string SystemResumen(string nombreTema, string? promptAlumno)
    {
        var estilo = string.IsNullOrWhiteSpace(promptAlumno)
            ? ObtenerEditable(ClaveResumen)
            : "Seguí ESTRICTAMENTE estas indicaciones del alumno para el contenido y el estilo del resumen " +
              "(tienen prioridad sobre cualquier estilo por defecto): " + promptAlumno.Trim();
        return $"{estilo} {Prompts.ResumenFijo} El tema de este resumen es: \"{nombreTema}\".";
    }
}
```

- [ ] **Step 5: Correr los tests y verificar que pasan**

Run: `dotnet test tests/Resumenes.Tests -c Debug --filter ServicioPromptsTests`
Expected: PASS (6 tests).

Nota: este paso deja `ConstructorPipeline.cs` con errores de compilación (todavía referencia `Prompts.LimpiezaSystem` y `Prompts.ResumenSystem`). Se corrige en Task 3; no compilar la solución completa aún.

- [ ] **Step 6: Commit**

```bash
git add src/Resumenes.Infrastructure/Aplicacion/ConstructorPipeline.cs \
        src/Resumenes.Infrastructure/Aplicacion/ServicioPrompts.cs \
        tests/Resumenes.Tests/ServicioPromptsTests.cs
git commit -m "feat(prompts): defaults neutros multi-idioma y ServicioPrompts"
```

---

## Task 3: Integrar `ServicioPrompts` en `ConstructorPipeline` (limpieza + resumen + idempotencia)

**Files:**
- Modify: `src/Resumenes.Infrastructure/Aplicacion/ConstructorPipeline.cs:11-13` (constructor), `:58-73` (LimpiezaIA), `:106-125` (ResumenFinal)
- Test: `tests/Resumenes.Tests/ServicioAnalisisTests.cs` (verificación de no-regresión)

**Interfaces:**
- Consumes: `ServicioPrompts.SystemLimpieza()`, `SystemResumen(...)`, `HashEditable(...)`, consts de clave (Task 2).
- Produces: `ConstructorPipeline(IRasterizador, IServicioOcr, IClienteIA, IGeneradorPdf, IConversorOffice, Configuracion, ServicioPrompts)`.

- [ ] **Step 1: Agregar `ServicioPrompts` al constructor**

En `ConstructorPipeline.cs`, cambiar la declaración primaria (líneas 11-13) a:

```csharp
public class ConstructorPipeline(
    IRasterizador rasterizador, IServicioOcr ocr, IClienteIA ia, IGeneradorPdf pdf,
    IConversorOffice conversor, Configuracion cfg, ServicioPrompts prompts)
{
```

- [ ] **Step 2: Usar el prompt y el hash editable en LimpiezaIA**

En el paso `LimpiezaIA` (≈líneas 58-73), reemplazar el `hash_entrada` y la llamada a la IA:

Cambiar:
```csharp
            new PasoPipeline(Etapa.LimpiezaIA, arc.Id, null, limpio,
                _ => Task.FromResult(Hashing.Sha256HexDeArchivo(bruto)),
```
por:
```csharp
            new PasoPipeline(Etapa.LimpiezaIA, arc.Id, null, limpio,
                _ => Task.FromResult(Hashing.Sha256HexDeTexto(
                    Hashing.Sha256HexDeArchivo(bruto) + "|" +
                    prompts.HashEditable(ServicioPrompts.ClaveLimpieza) + "|" + cfg.Modelo)),
```

Y cambiar la línea de la solicitud:
```csharp
                        var r = await ia.CompletarAsync(new SolicitudIA(
                            Prompts.LimpiezaSystem, bloque, 0.2, 8000, "limpieza-v1", cfg.Modelo), ctx.Ct);
```
por:
```csharp
                        var r = await ia.CompletarAsync(new SolicitudIA(
                            prompts.SystemLimpieza(), bloque, 0.2, 8000, "limpieza-v1", cfg.Modelo), ctx.Ct);
```

- [ ] **Step 3: Usar el prompt y el hash editable en ResumenFinal**

En el paso `ResumenFinal` (≈líneas 106-125), reemplazar el `hash_entrada`:

Cambiar:
```csharp
                _ => Task.FromResult(Hashing.Sha256HexDeTexto(
                    Hashing.Sha256HexDeArchivo(consolidado) + "|" + (promptResumen ?? ""))),
```
por:
```csharp
                _ => Task.FromResult(Hashing.Sha256HexDeTexto(
                    Hashing.Sha256HexDeArchivo(consolidado) + "|" + (promptResumen ?? "") + "|" +
                    prompts.HashEditable(ServicioPrompts.ClaveResumen))),
```

Y cambiar la composición del system + la llamada:
```csharp
                        var sys = Prompts.ResumenSystem(tema.Nombre, promptResumen);
                        var r = await ia.CompletarAsync(new SolicitudIA(
                            sys, bloque, 0.5, 8000, "resumen-v1", cfg.Modelo), ctx.Ct);
```
por:
```csharp
                        var sys = prompts.SystemResumen(tema.Nombre, promptResumen);
                        var r = await ia.CompletarAsync(new SolicitudIA(
                            sys, bloque, 0.5, 8000, "resumen-v1", cfg.Modelo), ctx.Ct);
```

- [ ] **Step 4: Correr los tests existentes de análisis (no-regresión)**

Run: `dotnet test tests/Resumenes.Tests -c Debug --filter ServicioAnalisisTests`
Expected: aún FALLA de compilación porque `ServicioAnalisis` construye `ConstructorPipeline` con la firma vieja. Se corrige en Task 4. (Si preferís, saltá la corrida hasta Task 4 Step 3.)

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Infrastructure/Aplicacion/ConstructorPipeline.cs
git commit -m "feat(prompts): pipeline usa ServicioPrompts y suma el prompt al hash de entrada"
```

---

## Task 4: Integrar en `DetectorTemas` y cablear en `ServicioAnalisis`

**Files:**
- Modify: `src/Resumenes.Infrastructure/Aplicacion/DetectorTemas.cs:11` (constructor), `:51-58` (system de detección)
- Modify: `src/Resumenes.Infrastructure/Aplicacion/ServicioAnalisis.cs:18` y `:112`
- Test: `tests/Resumenes.Tests/ServicioAnalisisTests.cs`

**Interfaces:**
- Consumes: `ServicioPrompts.SystemDeteccion(string)` (Task 2); `ConstructorPipeline(...,ServicioPrompts)` (Task 3).
- Produces: `DetectorTemas(IClienteIA, IRepositorioEstado, Configuracion, ServicioPrompts)`.

- [ ] **Step 1: Agregar `ServicioPrompts` a `DetectorTemas`**

En `DetectorTemas.cs`, cambiar la declaración (línea 11):

```csharp
public class DetectorTemas(IClienteIA ia, IRepositorioEstado repo, Configuracion cfg, ServicioPrompts prompts)
```

Y reemplazar el bloque que arma `sys` (líneas ≈51-56) por:

```csharp
        var sys = prompts.SystemDeteccion(promptTemas);
```

(borrando la concatenación literal anterior; el resto del método queda igual). La llamada `ia.CompletarAsync(new SolicitudIA(sys, sb.ToString(), 0.2, 4000, "deteccion-v1", cfg.Modelo), ct)` no cambia.

- [ ] **Step 2: Cablear en `ServicioAnalisis`**

En `ServicioAnalisis.cs`, línea 18, reemplazar la creación del pipeline e introducir el servicio de prompts:

Cambiar:
```csharp
    private readonly ConstructorPipeline _ctor = new(rasterizador, ocr, ia, generadorPdf, conversor, cfg);
```
por:
```csharp
    private readonly ServicioPrompts _prompts = new(repo);
    private readonly ConstructorPipeline _ctor = new(rasterizador, ocr, ia, generadorPdf, conversor, cfg, new ServicioPrompts(repo));
```

En la línea 112, cambiar:
```csharp
        var detector = new DetectorTemas(ia, repo, cfg);
```
por:
```csharp
        var detector = new DetectorTemas(ia, repo, cfg, _prompts);
```

- [ ] **Step 3: Correr toda la suite de tests**

Run: `dotnet test -c Debug`
Expected: PASS de todos los tests (los previos + `ServicioPromptsTests` + `AjustePrompt_...`). Los tests de `ServicioAnalisis` que usan `FakeClienteIA` (que devuelve `req.PromptUser`) siguen pasando porque la lógica de pasos no cambió, solo el system prompt.

- [ ] **Step 4: Compilar la solución completa (incluye Cli)**

Run: `dotnet build Resumenes.sln -c Debug`
Expected: BUILD succeeded. El `Resumenes.Cli` compila porque la clase `Prompts` sigue existiendo (alias `global using Prompts = ...`) y `Program.cs` no instancia `ConstructorPipeline` directamente.

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Infrastructure/Aplicacion/DetectorTemas.cs \
        src/Resumenes.Infrastructure/Aplicacion/ServicioAnalisis.cs
git commit -m "feat(prompts): deteccion usa ServicioPrompts y cableado en ServicioAnalisis"
```

---

## Task 5: ViewModel de Configuración — edición de prompts

**Files:**
- Modify: `src/Resumenes.Ui/ViewModels/ConfiguracionVm.cs`
- Modify: `src/Resumenes.Ui/App.xaml.cs:169` (registro de `ConfiguracionVm`) y sección de servicios (registrar `ServicioPrompts`)
- Test: `tests/Resumenes.Ui.Tests/ConfiguracionVmPromptsTests.cs`

**Interfaces:**
- Consumes: `ServicioPrompts` (Task 2).
- Produces: `ConfiguracionVm(IAlmacenSecretos, Configuracion, ServicioPrompts)`; props `PromptLimpieza`, `PromptDeteccion`, `PromptResumen` (string, TwoWay); comandos `GuardarPromptsCommand`, `RestaurarPromptsCommand`; props de solo lectura `FormatoLimpieza`, `FormatoDeteccion`, `FormatoResumen`.

- [ ] **Step 1: Escribir el test que falla**

Crear `tests/Resumenes.Ui.Tests/ConfiguracionVmPromptsTests.cs`:

```csharp
using Resumenes.Infrastructure.Aplicacion;
using Resumenes.Tests.Fakes;
using Resumenes.Ui.ViewModels;
using Xunit;

namespace Resumenes.Ui.Tests;

public class ConfiguracionVmPromptsTests
{
    private sealed class SecretosFake : Resumenes.Core.Interfaces.IAlmacenSecretos
    {
        public string? ObtenerApiKey() => null;
        public void GuardarApiKey(string apiKey) { }
    }

    private static ConfiguracionVm Nuevo(out ServicioPrompts sp)
    {
        sp = new ServicioPrompts(new RepositorioEnMemoria());
        return new ConfiguracionVm(new SecretosFake(), new Configuracion(), sp);
    }

    [Fact]
    public void Carga_LosPromptsEditablesPorDefecto()
    {
        var vm = Nuevo(out _);
        Assert.Equal(Prompts.ResumenEditableDefault, vm.PromptResumen);
        Assert.Contains("#TITULO:", vm.FormatoResumen); // parte fija visible (solo lectura)
    }

    [Fact]
    public void GuardarPrompts_PersisteElOverride()
    {
        var vm = Nuevo(out var sp);
        vm.PromptResumen = "Mi estilo propio";
        vm.GuardarPromptsCommand.Execute(null);
        Assert.Equal("Mi estilo propio", sp.ObtenerEditable(ServicioPrompts.ClaveResumen));
    }

    [Fact]
    public void RestaurarPrompts_VuelveAlDefault()
    {
        var vm = Nuevo(out var sp);
        vm.PromptResumen = "algo";
        vm.GuardarPromptsCommand.Execute(null);
        vm.RestaurarPromptsCommand.Execute(null);
        Assert.Equal(Prompts.ResumenEditableDefault, vm.PromptResumen);
        Assert.Equal(Prompts.ResumenEditableDefault, sp.ObtenerEditable(ServicioPrompts.ClaveResumen));
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test tests/Resumenes.Ui.Tests -c Debug --filter ConfiguracionVmPromptsTests`
Expected: FAIL de compilación (el constructor de `ConfiguracionVm` no recibe `ServicioPrompts`; faltan props/comandos).

- [ ] **Step 3: Modificar `ConfiguracionVm`**

En `ConfiguracionVm.cs`:

Agregar el campo y cambiar el constructor:
```csharp
    private readonly ServicioPrompts _prompts;
```
```csharp
    public ConfiguracionVm(IAlmacenSecretos secretos, Configuracion cfg, ServicioPrompts prompts)
    {
        _secretos = secretos;
        _cfg = cfg;
        _prompts = prompts;
        Cargar();
    }
```

Agregar las propiedades observables (junto a las demás `[ObservableProperty]`):
```csharp
    // ── Prompts editables (rol/estilo) ──
    [ObservableProperty] private string _promptLimpieza = string.Empty;
    [ObservableProperty] private string _promptDeteccion = string.Empty;
    [ObservableProperty] private string _promptResumen = string.Empty;

    // Partes fijas (solo lectura, para mostrar contexto en la UI)
    public string FormatoLimpieza => Prompts.LimpiezaFijo;
    public string FormatoDeteccion => Prompts.DeteccionFijo;
    public string FormatoResumen => Prompts.ResumenFijo;
```

En `Cargar()`, al final, agregar:
```csharp
        PromptLimpieza = _prompts.ObtenerEditable(ServicioPrompts.ClaveLimpieza);
        PromptDeteccion = _prompts.ObtenerEditable(ServicioPrompts.ClaveDeteccion);
        PromptResumen = _prompts.ObtenerEditable(ServicioPrompts.ClaveResumen);
```

Agregar los comandos (junto a los demás `[RelayCommand]`):
```csharp
    [RelayCommand]
    private void GuardarPrompts()
    {
        try
        {
            _prompts.GuardarEditable(ServicioPrompts.ClaveLimpieza, PromptLimpieza.Trim());
            _prompts.GuardarEditable(ServicioPrompts.ClaveDeteccion, PromptDeteccion.Trim());
            _prompts.GuardarEditable(ServicioPrompts.ClaveResumen, PromptResumen.Trim());
            MensajeEstado = "Prompts guardados. Los próximos análisis (o regeneraciones) los usarán.";
        }
        catch (Exception ex)
        {
            MensajeEstado = $"Error al guardar los prompts: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RestaurarPrompts()
    {
        _prompts.RestaurarDefault(ServicioPrompts.ClaveLimpieza);
        _prompts.RestaurarDefault(ServicioPrompts.ClaveDeteccion);
        _prompts.RestaurarDefault(ServicioPrompts.ClaveResumen);
        PromptLimpieza = _prompts.ObtenerEditable(ServicioPrompts.ClaveLimpieza);
        PromptDeteccion = _prompts.ObtenerEditable(ServicioPrompts.ClaveDeteccion);
        PromptResumen = _prompts.ObtenerEditable(ServicioPrompts.ClaveResumen);
        MensajeEstado = "Prompts restaurados a los valores por defecto.";
    }
```

Agregar el `using` necesario arriba si falta:
```csharp
using Resumenes.Infrastructure.Aplicacion;
```
(ya está presente — verificar.)

- [ ] **Step 4: Registrar en DI**

En `src/Resumenes.Ui/App.xaml.cs`, en la sección de servicios (cerca de la línea 137-142), agregar el registro de `ServicioPrompts`:
```csharp
        sc.AddSingleton<ServicioPrompts>(sp =>
            new ServicioPrompts(sp.GetRequiredService<SqliteRepositorioEstado>()));
```

Y reemplazar el registro de `ConfiguracionVm` (línea 169) por un factory:
```csharp
        sc.AddTransient<ConfiguracionVm>(sp => new ConfiguracionVm(
            sp.GetRequiredService<IAlmacenSecretos>(),
            sp.GetRequiredService<Configuracion>(),
            sp.GetRequiredService<ServicioPrompts>()));
```

Agregar el `using Resumenes.Infrastructure.Aplicacion;` en App.xaml.cs si no estuviera (ya se usa `Configuracion`, debería estar).

- [ ] **Step 5: Correr el test y verificar que pasa**

Run: `dotnet test tests/Resumenes.Ui.Tests -c Debug --filter ConfiguracionVmPromptsTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/Resumenes.Ui/ViewModels/ConfiguracionVm.cs src/Resumenes.Ui/App.xaml.cs \
        tests/Resumenes.Ui.Tests/ConfiguracionVmPromptsTests.cs
git commit -m "feat(prompts): ConfiguracionVm edita/guarda/restaura prompts + DI"
```

---

## Task 6: UI XAML — sección "Prompts (avanzado)" y verificación final

**Files:**
- Modify: `src/Resumenes.Ui/Vistas/VistaConfiguracion.xaml`
- (Verificación manual + build de solución)

**Interfaces:**
- Consumes: bindings de Task 5 (`PromptLimpieza/Deteccion/Resumen`, `Formato*`, `GuardarPromptsCommand`, `RestaurarPromptsCommand`).

- [ ] **Step 1: Insertar la sección de prompts**

En `VistaConfiguracion.xaml`, insertar el siguiente bloque ANTES del botón "Guardar configuración" (antes de la línea `<!-- ── Botón Guardar configuración ── -->`, ≈línea 186):

```xml
      <!-- ── Prompts (avanzado) ── -->
      <ui:Card Padding="20" Margin="0,0,0,16">
        <StackPanel>
          <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
            <ui:SymbolIcon Symbol="Wand24" Margin="0,0,8,0"
                           Foreground="{DynamicResource SystemAccentColorBrush}"/>
            <TextBlock Text="Prompts (avanzado)"
                       FontSize="14" FontWeight="SemiBold"
                       Foreground="{DynamicResource TextFillColorPrimaryBrush}"
                       VerticalAlignment="Center"/>
          </StackPanel>
          <TextBlock Text="Estos prompts definen el comportamiento central de la IA. Editás solo el rol/estilo; el formato de salida (que la app necesita para generar el PDF y los temas) se aplica automáticamente y no se puede romper."
                     FontSize="12" TextWrapping="Wrap"
                     Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                     Margin="0,0,0,12"/>

          <!-- Limpieza -->
          <TextBlock Text="Corrección de OCR (limpieza)" FontSize="13" FontWeight="SemiBold"
                     Foreground="{DynamicResource TextFillColorPrimaryBrush}" Margin="0,0,0,4"/>
          <ui:TextBox Text="{Binding PromptLimpieza, UpdateSourceTrigger=PropertyChanged}"
                      AcceptsReturn="True" TextWrapping="Wrap" MinHeight="60" FontSize="12"/>
          <TextBlock Text="{Binding FormatoLimpieza}" FontSize="11" TextWrapping="Wrap"
                     Foreground="{DynamicResource TextFillColorTertiaryBrush}" Margin="0,4,0,12"/>

          <!-- Detección -->
          <TextBlock Text="Detección de temas" FontSize="13" FontWeight="SemiBold"
                     Foreground="{DynamicResource TextFillColorPrimaryBrush}" Margin="0,0,0,4"/>
          <ui:TextBox Text="{Binding PromptDeteccion, UpdateSourceTrigger=PropertyChanged}"
                      AcceptsReturn="True" TextWrapping="Wrap" MinHeight="60" FontSize="12"/>
          <TextBlock Text="El formato JSON de salida se aplica automáticamente."
                     FontSize="11" TextWrapping="Wrap"
                     Foreground="{DynamicResource TextFillColorTertiaryBrush}" Margin="0,4,0,12"/>

          <!-- Resumen -->
          <TextBlock Text="Resumen" FontSize="13" FontWeight="SemiBold"
                     Foreground="{DynamicResource TextFillColorPrimaryBrush}" Margin="0,0,0,4"/>
          <ui:TextBox Text="{Binding PromptResumen, UpdateSourceTrigger=PropertyChanged}"
                      AcceptsReturn="True" TextWrapping="Wrap" MinHeight="60" FontSize="12"/>
          <TextBlock Text="{Binding FormatoResumen}" FontSize="11" TextWrapping="Wrap"
                     Foreground="{DynamicResource TextFillColorTertiaryBrush}" Margin="0,4,0,12"/>

          <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
            <ui:Button Content="Restaurar por defecto"
                       Command="{Binding RestaurarPromptsCommand}"
                       Icon="{ui:SymbolIcon Symbol=ArrowReset24}" Margin="0,0,8,0"/>
            <ui:Button Content="Guardar prompts"
                       Command="{Binding GuardarPromptsCommand}"
                       Appearance="Primary"
                       Icon="{ui:SymbolIcon Symbol=Save24}"/>
          </StackPanel>
        </StackPanel>
      </ui:Card>
```

- [ ] **Step 2: Compilar la solución**

Run: `dotnet build Resumenes.sln -c Debug`
Expected: BUILD succeeded (0 errores). Si algún `Symbol` (`Wand24`, `ArrowReset24`) no existe en la versión de WPF-UI, reemplazarlo por `Settings24` / `ArrowCounterclockwise24` respectivamente.

- [ ] **Step 3: Correr toda la suite de tests**

Run: `dotnet test -c Debug`
Expected: PASS de todo (suite previa + nuevos tests de esta fase).

- [ ] **Step 4: Verificación manual (el usuario prueba la UI)**

Cerrar cualquier instancia de `Resumenes.Ui` (la DLL queda bloqueada). Ejecutar la app, ir a Configuración → sección "Prompts (avanzado)":
1. Se ven los 3 prompts con su texto por defecto (neutros, sin "español").
2. Editar el de resumen, "Guardar prompts" → reabrir Configuración: el texto editado persiste.
3. "Restaurar por defecto" → vuelven a los textos por defecto.
4. (Opcional) Procesar material en inglés/portugués y confirmar que el resumen sale en ese idioma.

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Ui/Vistas/VistaConfiguracion.xaml
git commit -m "feat(prompts): seccion Prompts (avanzado) en Configuracion"
```

---

## Self-Review (cobertura de la spec, Fase 1)

- **#1 prompts editables (rol/estilo, formato protegido, restaurar default):** Tasks 2, 5, 6. ✅
- **#1 persistencia override en SQLite (`AjustePrompt`):** Task 1. ✅
- **#1 idempotencia (hash de entrada incluye el prompt):** Task 3 (limpieza y resumen). Detección no es unidad cacheada por hash — documentado en la spec §1. ✅
- **#2 multi-idioma (defaults neutros, autodetección):** Task 2 (defaults), Task 3-4 (uso). ✅
- **Migración no destructiva (`schema_version`→2, `IF NOT EXISTS`):** Task 1. ✅
- **Build de la solución (incluye Cli):** Task 4 Step 4, Task 6 Step 2. ✅

## Notas de diseño

- La clase `Prompts` se conserva (no se elimina) para no romper el alias `global using Prompts = ...` de `Resumenes.Cli`; ahora expone los defaults neutros y las partes fijas como constantes públicas.
- Editar el prompt de **detección** no fuerza re-detección automática (depende de `temas.json`); el usuario puede borrar ese archivo para re-detectar. Igual que en la spec.
- La caché global (Fase 2) reutilizará `ServicioPrompts.HashEditable(ClaveLimpieza)` como parte de la clave de variante de la limpieza.
