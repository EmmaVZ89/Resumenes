# Fase 4 — Quitar archivos antes de procesar + "Acerca de"/LinkedIn — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permitir excluir archivos detectados antes de procesar un análisis (la exclusión persiste y se respeta al reanudar), y agregar una sección "Acerca de" en Configuración con el LinkedIn del autor.

**Architecture:** Las exclusiones se persisten por carpeta de origen en SQLite (`ExclusionArchivo`), evitando el ciclo fingerprint↔id: `AbrirOCrearAsync` recibe las exclusiones explícitas (al crear) o las lee por carpeta (al reanudar), filtra el set y calcula el fingerprint sobre los incluidos. La UI de "Nuevo análisis" muestra los archivos candidatos (los que el servicio realmente procesa: nivel superior) con checkboxes. "Acerca de" es una sección estática en Configuración con un botón que abre el perfil de LinkedIn.

**Tech Stack:** .NET 9, C#, Microsoft.Data.Sqlite, WPF / WPF-UI 4.x, CommunityToolkit.Mvvm, xUnit.

## Global Constraints

- **Plataforma:** Windows-only; datos por usuario en `%LOCALAPPDATA%/ResumenesApp/`.
- **Alcance de carpeta:** SOLO nivel superior (`Directory.GetFiles`, sin subcarpetas), con extensiones `.pdf .doc .docx .ppt .pptx .txt`. La lista mostrada debe coincidir exactamente con lo que el servicio procesa.
- **Originales intactos:** excluir un archivo solo lo omite del procesamiento; nunca se borra ni se mueve.
- **Exclusión persistente:** se guarda por `carpeta_origen` (ruta absoluta) para que reanudar/re-procesar respete la misma selección y produzca el mismo fingerprint (evita análisis duplicados).
- **Fingerprint:** se calcula sobre el set de archivos INCLUIDOS.
- **SQLite es índice de estado.** Migración no destructiva: `schema_version`→5; `CREATE TABLE IF NOT EXISTS`; bump con upsert.
- **LinkedIn:** `https://ar.linkedin.com/in/emmanuel-zelarayan/es` — abrir en el navegador con `Process.Start(UseShellExecute=true)`.
- **Build de verificación:** `dotnet build Resumenes.sln -c Debug` debe pasar (incluye `Resumenes.Cli`).
- **Tests:** xUnit + fakes (`RepositorioEnMemoria`).

---

## File Structure

- `src/Resumenes.Infrastructure/schema.sql` + `schema.sql` — **Modify**: tabla `ExclusionArchivo`, `schema_version`→5.
- `src/Resumenes.Core/Interfaces/IRepositorioEstado.cs` — **Modify**: `ObtenerExclusiones`/`GuardarExclusiones`.
- `src/Resumenes.Infrastructure/Persistencia/SqliteRepositorioEstado.cs` — **Modify**: implementación.
- `tests/Resumenes.Tests/Fakes/Fakes.cs` + los 2 `RepoFake` de `Resumenes.Ui.Tests` — **Modify**: implementar los 2 métodos.
- `src/Resumenes.Core/Interfaces/IServicioAnalisis.cs` — **Modify**: `ListarArchivosCandidatos`; `AbrirOCrearAsync` con `rutasExcluidas` opcional.
- `src/Resumenes.Infrastructure/Aplicacion/ServicioAnalisis.cs` — **Modify**: filtrar/persistir exclusiones; listar candidatos.
- `src/Resumenes.Ui/ViewModels/ArchivoSeleccionableVm.cs` — **Create**: ítem con checkbox.
- `src/Resumenes.Ui/ViewModels/ConfigurarVm.cs` — **Modify**: usar candidatos del servicio + checkboxes + pasar excluidos.
- `src/Resumenes.Ui/Vistas/VistaConfigurar.xaml` — **Modify**: lista con checkboxes.
- `src/Resumenes.Ui/ViewModels/ConfiguracionVm.cs` — **Modify**: comando AbrirLinkedIn + versión.
- `src/Resumenes.Ui/Vistas/VistaConfiguracion.xaml` — **Modify**: sección "Acerca de".
- Tests: `tests/Resumenes.Tests/SqliteRepositorioEstadoTests.cs`, `tests/Resumenes.Tests/ExclusionArchivosTests.cs`.

---

## Task 1: Persistir exclusiones por carpeta

**Files:**
- Modify: `src/Resumenes.Infrastructure/schema.sql`, `schema.sql`
- Modify: `src/Resumenes.Core/Interfaces/IRepositorioEstado.cs`
- Modify: `src/Resumenes.Infrastructure/Persistencia/SqliteRepositorioEstado.cs`
- Modify: `tests/Resumenes.Tests/Fakes/Fakes.cs`, `tests/Resumenes.Ui.Tests/ConfirmarTemasVmTests.cs`, `tests/Resumenes.Ui.Tests/InicioVmTests.cs`
- Test: `tests/Resumenes.Tests/SqliteRepositorioEstadoTests.cs`

**Interfaces:**
- Produces:
  - `IReadOnlyCollection<string> IRepositorioEstado.ObtenerExclusiones(string carpetaOrigen)`
  - `void IRepositorioEstado.GuardarExclusiones(string carpetaOrigen, IReadOnlyCollection<string> rutasRelativas)` (reemplaza el set de esa carpeta)

- [ ] **Step 1: Escribir el test que falla**

En `tests/Resumenes.Tests/SqliteRepositorioEstadoTests.cs`, agregar (patrón de archivo temporal):

```csharp
    [Fact]
    public void Exclusiones_GuardarReemplazaYObtiene()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"resu-{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteRepositorioEstado($"Data Source={tmp}");
            repo.InicializarEsquema();

            Assert.Empty(repo.ObtenerExclusiones(@"C:\mat"));

            repo.GuardarExclusiones(@"C:\mat", new[] { "a.pdf", "b.pdf" });
            Assert.Equal(new[] { "a.pdf", "b.pdf" }, repo.ObtenerExclusiones(@"C:\mat").OrderBy(x => x));

            // Reemplazo: guardar otro set pisa el anterior
            repo.GuardarExclusiones(@"C:\mat", new[] { "c.pdf" });
            Assert.Equal(new[] { "c.pdf" }, repo.ObtenerExclusiones(@"C:\mat"));

            // No afecta a otra carpeta
            Assert.Empty(repo.ObtenerExclusiones(@"C:\otra"));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test tests/Resumenes.Tests -c Debug --filter Exclusiones_GuardarReemplazaYObtiene`
Expected: FAIL de compilación (`ObtenerExclusiones` no existe).

- [ ] **Step 3: Agregar la tabla al esquema y subir la versión**

En `src/Resumenes.Infrastructure/schema.sql`, antes del bloque `SchemaMeta`, insertar:

```sql
-- ----------------------------------------------------------------------------
-- EXCLUSION_ARCHIVO: archivos que el usuario excluyó del procesamiento, por carpeta.
--   Se respeta al reanudar (mismo set incluido ⇒ mismo fingerprint).
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ExclusionArchivo (
    carpeta_origen TEXT NOT NULL,
    ruta_relativa  TEXT NOT NULL,
    PRIMARY KEY (carpeta_origen, ruta_relativa)
);
```

Y subir la versión (la línea que hoy dice `'4'`):
```sql
INSERT INTO SchemaMeta (clave, valor) VALUES ('schema_version', '5')
ON CONFLICT(clave) DO UPDATE SET valor='5';
```

Replicar en `schema.sql` de la raíz.

- [ ] **Step 4: Declarar los métodos en la interfaz**

En `src/Resumenes.Core/Interfaces/IRepositorioEstado.cs`, antes del cierre:

```csharp
    /// <summary>Rutas relativas excluidas del procesamiento para una carpeta de origen.</summary>
    IReadOnlyCollection<string> ObtenerExclusiones(string carpetaOrigen);
    /// <summary>Reemplaza el conjunto de exclusiones de una carpeta por el indicado.</summary>
    void GuardarExclusiones(string carpetaOrigen, IReadOnlyCollection<string> rutasRelativas);
```

- [ ] **Step 5: Implementar en SqliteRepositorioEstado**

Agregar antes del cierre de la clase:

```csharp
    public IReadOnlyCollection<string> ObtenerExclusiones(string carpetaOrigen)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT ruta_relativa FROM ExclusionArchivo WHERE carpeta_origen=$c;";
        cmd.Parameters.AddWithValue("$c", carpetaOrigen);
        using var r = cmd.ExecuteReader();
        var lista = new List<string>();
        while (r.Read()) lista.Add(r.GetString(0));
        return lista;
    }

    public void GuardarExclusiones(string carpetaOrigen, IReadOnlyCollection<string> rutasRelativas)
    {
        using var con = Abrir();
        using var tx = con.BeginTransaction();
        using (var del = con.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM ExclusionArchivo WHERE carpeta_origen=$c;";
            del.Parameters.AddWithValue("$c", carpetaOrigen);
            del.ExecuteNonQuery();
        }
        foreach (var ruta in rutasRelativas)
        {
            using var ins = con.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "INSERT OR IGNORE INTO ExclusionArchivo (carpeta_origen, ruta_relativa) VALUES ($c,$r);";
            ins.Parameters.AddWithValue("$c", carpetaOrigen);
            ins.Parameters.AddWithValue("$r", ruta);
            ins.ExecuteNonQuery();
        }
        tx.Commit();
    }
```

- [ ] **Step 6: Actualizar fakes / impls**

En `tests/Resumenes.Tests/Fakes/Fakes.cs` (`RepositorioEnMemoria`):
```csharp
    private readonly Dictionary<string, List<string>> _exclusiones = new();
    public IReadOnlyCollection<string> ObtenerExclusiones(string carpetaOrigen)
        => _exclusiones.TryGetValue(carpetaOrigen, out var l) ? l.ToList() : (IReadOnlyCollection<string>)Array.Empty<string>();
    public void GuardarExclusiones(string carpetaOrigen, IReadOnlyCollection<string> rutasRelativas)
        => _exclusiones[carpetaOrigen] = rutasRelativas.ToList();
```

En los `RepoFake` de `ConfirmarTemasVmTests.cs` e `InicioVmTests.cs`:
```csharp
    public IReadOnlyCollection<string> ObtenerExclusiones(string carpetaOrigen) => Array.Empty<string>();
    public void GuardarExclusiones(string carpetaOrigen, IReadOnlyCollection<string> rutasRelativas) { }
```

- [ ] **Step 7: Correr el test y verificar que pasa**

Run: `dotnet test tests/Resumenes.Tests -c Debug --filter Exclusiones_GuardarReemplazaYObtiene`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Resumenes.Infrastructure/schema.sql schema.sql \
        src/Resumenes.Core/Interfaces/IRepositorioEstado.cs \
        src/Resumenes.Infrastructure/Persistencia/SqliteRepositorioEstado.cs \
        tests/Resumenes.Tests/Fakes/Fakes.cs \
        tests/Resumenes.Ui.Tests/ConfirmarTemasVmTests.cs tests/Resumenes.Ui.Tests/InicioVmTests.cs \
        tests/Resumenes.Tests/SqliteRepositorioEstadoTests.cs
git commit -m "feat(archivos): persistir exclusiones de archivos por carpeta"
```

---

## Task 2: Servicio — listar candidatos y excluir en `AbrirOCrearAsync`

**Files:**
- Modify: `src/Resumenes.Core/Interfaces/IServicioAnalisis.cs`
- Modify: `src/Resumenes.Infrastructure/Aplicacion/ServicioAnalisis.cs`
- Test: `tests/Resumenes.Tests/ExclusionArchivosTests.cs`

**Interfaces:**
- Consumes: `IRepositorioEstado.ObtenerExclusiones/GuardarExclusiones` (Task 1).
- Produces:
  - `IReadOnlyList<string> IServicioAnalisis.ListarArchivosCandidatos(string carpeta)` (nombres de archivo de nivel superior, extensiones aceptadas, ordenados)
  - `IServicioAnalisis.AbrirOCrearAsync(string carpeta, CancellationToken ct, IReadOnlyCollection<string>? rutasExcluidas = null)`

- [ ] **Step 1: Escribir los tests que fallan**

Crear `tests/Resumenes.Tests/ExclusionArchivosTests.cs`:

```csharp
using Resumenes.Core.Modelos;
using Resumenes.Tests.Fakes;
using Xunit;

namespace Resumenes.Tests;

public class ExclusionArchivosTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), $"resu-excl-{Guid.NewGuid():N}");

    private string CarpetaConDos()
    {
        var carpeta = Path.Combine(_base, "material");
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(Path.Combine(carpeta, "a.txt"), "contenido a");
        File.WriteAllText(Path.Combine(carpeta, "b.txt"), "contenido b");
        return carpeta;
    }

    [Fact]
    public void ListarArchivosCandidatos_DevuelveLosDosTxt()
    {
        var carpeta = CarpetaConDos();
        var svc = ServicioAnalisisFactory.ParaTests(new RepositorioEnMemoria(), Path.Combine(_base, "ws"));
        var candidatos = svc.ListarArchivosCandidatos(carpeta);
        Assert.Equal(new[] { "a.txt", "b.txt" }, candidatos.OrderBy(x => x));
    }

    [Fact]
    public async Task AbrirOCrear_ConExclusion_ProcesaSoloIncluidos_yPersiste()
    {
        var carpeta = CarpetaConDos();
        var repo = new RepositorioEnMemoria();
        var svc = ServicioAnalisisFactory.ParaTests(repo, Path.Combine(_base, "ws"));

        var an = await svc.AbrirOCrearAsync(carpeta, default, new[] { "b.txt" });
        await svc.ProcesarArchivosAsync(an, null, default);

        // Solo se registró/procesó a.txt (b.txt excluido)
        Assert.Equal(new[] { "b.txt" }, repo.ObtenerExclusiones(Path.GetFullPath(carpeta)));

        // Reanudar SIN pasar exclusiones: debe leer las persistidas y dar el MISMO análisis
        var an2 = await svc.AbrirOCrearAsync(carpeta, default);
        Assert.Equal(an.Id, an2.Id);   // mismo fingerprint ⇒ mismo análisis (no duplica)
    }

    public void Dispose()
    {
        if (Directory.Exists(_base)) Directory.Delete(_base, true);
    }
}
```

- [ ] **Step 2: Correr y verificar que fallan**

Run: `dotnet test tests/Resumenes.Tests -c Debug --filter ExclusionArchivosTests`
Expected: FAIL de compilación (`ListarArchivosCandidatos` y el overload con `rutasExcluidas` no existen).

- [ ] **Step 3: Actualizar la interfaz**

En `src/Resumenes.Core/Interfaces/IServicioAnalisis.cs`, cambiar la firma de `AbrirOCrearAsync` y agregar `ListarArchivosCandidatos`:

```csharp
    Task<Analisis> AbrirOCrearAsync(string carpeta, CancellationToken ct, IReadOnlyCollection<string>? rutasExcluidas = null);
    /// <summary>Nombres de archivo (nivel superior, extensiones aceptadas) candidatos a procesar.</summary>
    IReadOnlyList<string> ListarArchivosCandidatos(string carpeta);
```

- [ ] **Step 4: Implementar en `ServicioAnalisis`**

En `ServicioAnalisis.cs`:

Agregar el método de candidatos:
```csharp
    public IReadOnlyList<string> ListarArchivosCandidatos(string carpeta)
    {
        if (!Directory.Exists(carpeta)) return Array.Empty<string>();
        return Directory.GetFiles(carpeta)
            .Where(f => _exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(p => p)
            .Select(Path.GetFileName)
            .ToList()!;
    }
```

Cambiar `AbrirOCrearAsync` para aceptar y aplicar las exclusiones. Reemplazar la firma y el inicio del método:

```csharp
    public Task<Analisis> AbrirOCrearAsync(string carpeta, CancellationToken ct, IReadOnlyCollection<string>? rutasExcluidas = null)
    {
        var carpetaAbs = Path.GetFullPath(carpeta);

        // Persistir las exclusiones explícitas (creación); si no se pasan, usar las guardadas (reanudar).
        if (rutasExcluidas != null)
            repo.GuardarExclusiones(carpetaAbs, rutasExcluidas);
        var excluidos = (rutasExcluidas ?? repo.ObtenerExclusiones(carpetaAbs))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var archivos = Directory.GetFiles(carpeta)
            .Where(f => _exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Where(f => !excluidos.Contains(Path.GetFileName(f)))
            .OrderBy(p => p).ToArray();

        var hashes = archivos.ToDictionary(p => p, Hashing.Sha256HexDeArchivo);
        var fingerprint = Hashing.Sha256HexDeTexto(string.Join("|",
            archivos.Select(p => Path.GetFileName(p) + ":" + hashes[p])));
        var nombreAnalisis = Path.GetFileName(carpeta.TrimEnd('\\', '/'));

        var an = repo.ObtenerAnalisisPorFingerprint(fingerprint)
            ?? new Analisis(Ids.SlugId(nombreAnalisis), nombreAnalisis, carpetaAbs, fingerprint,
                EstadoAnalisis.EnProceso, reloj.Ahora(), reloj.Ahora());
        repo.GuardarAnalisis(an);

        _archivosActuales = archivos;
        _hashesActuales = hashes;

        return Task.FromResult(an);
    }
```

(Es el mismo cuerpo previo, con: `carpetaAbs` reutilizado, el filtro de exclusiones, y `CarpetaOrigen = carpetaAbs`. El resto del servicio no cambia.)

- [ ] **Step 5: Correr los tests y verificar que pasan**

Run: `dotnet test tests/Resumenes.Tests -c Debug --filter ExclusionArchivosTests`
Expected: PASS (2 tests).

- [ ] **Step 6: Correr la suite completa (no-regresión de call sites)**

Run: `dotnet test -c Debug`
Expected: verde. Los call sites existentes de `AbrirOCrearAsync(carpeta, ct)` siguen compilando (el 3.er parámetro es opcional) y se comportan igual (sin exclusiones explícitas → leen las persistidas, que estarán vacías salvo que el usuario haya excluido).

- [ ] **Step 7: Commit**

```bash
git add src/Resumenes.Core/Interfaces/IServicioAnalisis.cs \
        src/Resumenes.Infrastructure/Aplicacion/ServicioAnalisis.cs \
        tests/Resumenes.Tests/ExclusionArchivosTests.cs
git commit -m "feat(archivos): listar candidatos y excluir archivos en AbrirOCrearAsync"
```

---

## Task 3: UI — checkboxes para excluir archivos

**Files:**
- Create: `src/Resumenes.Ui/ViewModels/ArchivoSeleccionableVm.cs`
- Modify: `src/Resumenes.Ui/ViewModels/ConfigurarVm.cs`
- Modify: `src/Resumenes.Ui/Vistas/VistaConfigurar.xaml`
- Test: `tests/Resumenes.Ui.Tests/ConfigurarVmTests.cs`

**Interfaces:**
- Consumes: `IServicioAnalisis.ListarArchivosCandidatos`, `AbrirOCrearAsync(..., rutasExcluidas)` (Task 2).
- Produces: `ArchivoSeleccionableVm { string Nombre; bool Incluido }`; `ConfigurarVm.Archivos` pasa a `ObservableCollection<ArchivoSeleccionableVm>`.

- [ ] **Step 1: Escribir el test que falla**

Crear `tests/Resumenes.Ui.Tests/ConfigurarVmTests.cs`. Verifica que al analizar se pasan como excluidos los desmarcados. Usar un fake de `IServicioAnalisis` que capture las exclusiones:

```csharp
using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;
using Resumenes.Core.Orquestacion;
using Resumenes.Ui.ViewModels;
using Xunit;

namespace Resumenes.Ui.Tests;

public class ConfigurarVmTests
{
    private sealed class ServicioFake : IServicioAnalisis
    {
        public IReadOnlyCollection<string>? ExcluidosRecibidos;
        public IReadOnlyList<string> Candidatos = new List<string> { "a.txt", "b.txt" };

        public IReadOnlyList<string> ListarArchivosCandidatos(string carpeta) => Candidatos;
        public Task<Analisis> AbrirOCrearAsync(string carpeta, CancellationToken ct, IReadOnlyCollection<string>? rutasExcluidas = null)
        {
            ExcluidosRecibidos = rutasExcluidas;
            return Task.FromResult(new Analisis("id","n",carpeta,"fp",EstadoAnalisis.EnProceso,DateTime.UtcNow,DateTime.UtcNow));
        }
        public Task<ResultadoLote> ProcesarArchivosAsync(Analisis an, IProgress<ProgresoPaso>? p, CancellationToken ct) => Task.FromResult(new ResultadoLote(0,0,Array.Empty<string>()));
        public Task<IReadOnlyList<TemaDetectado>> DetectarTemasAsync(Analisis an, string prompt, CancellationToken ct) => Task.FromResult<IReadOnlyList<TemaDetectado>>(Array.Empty<TemaDetectado>());
        public Task<ResultadoLote> GenerarPorTemasAsync(Analisis an, IReadOnlyList<TemaDetectado> t, string prompt, IProgress<ProgresoPaso>? p, CancellationToken ct) => Task.FromResult(new ResultadoLote(0,0,Array.Empty<string>()));
    }

    [Fact]
    public void CargarCandidatos_PoblaArchivosTodosIncluidos()
    {
        var svc = new ServicioFake();
        var vm = new ConfigurarVm(svc, null!);
        vm.CargarCandidatosParaTest(@"C:\mat");
        Assert.Equal(2, vm.Archivos.Count);
        Assert.All(vm.Archivos, a => Assert.True(a.Incluido));
    }

    [Fact]
    public async Task Analizar_PasaLosDesmarcadosComoExcluidos()
    {
        var svc = new ServicioFake();
        var vm = new ConfigurarVm(svc, null!);
        vm.CargarCandidatosParaTest(@"C:\mat");
        vm.CarpetaSeleccionada = @"C:\mat";
        vm.Archivos.First(a => a.Nombre == "b.txt").Incluido = false;

        await vm.AnalizarParaTestAsync();

        Assert.Equal(new[] { "b.txt" }, svc.ExcluidosRecibidos);
    }
}
```

(El VM expone helpers internos para test: `CargarCandidatosParaTest(string)` que llama al mismo código que `SeleccionarCarpeta`, y `AnalizarParaTestAsync()` que ejecuta el cuerpo del comando `Analizar` sin navegación. Implementarlos en el Step 3.)

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test tests/Resumenes.Ui.Tests -c Debug --filter ConfigurarVmTests`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `ArchivoSeleccionableVm` y actualizar `ConfigurarVm`**

Crear `src/Resumenes.Ui/ViewModels/ArchivoSeleccionableVm.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Resumenes.Ui.ViewModels;

/// <summary>Archivo candidato a procesar, con checkbox de inclusión.</summary>
public partial class ArchivoSeleccionableVm : ObservableObject
{
    public required string Nombre { get; init; }
    [ObservableProperty] private bool _incluido = true;
}
```

En `ConfigurarVm.cs`:
- Cambiar `Archivos` a `ObservableCollection<ArchivoSeleccionableVm>`.
- Reemplazar `CargarArchivos` para usar el servicio (nivel superior, coincide con lo procesado):
```csharp
    private void CargarArchivos(string carpeta)
    {
        Archivos.Clear();
        foreach (var nombre in _servicio.ListarArchivosCandidatos(carpeta))
            Archivos.Add(new ArchivoSeleccionableVm { Nombre = nombre });
    }
```
- En `Analizar`, recolectar los excluidos y pasarlos:
```csharp
            var excluidos = Archivos.Where(a => !a.Incluido).Select(a => a.Nombre).ToList();
            var an = await _servicio.AbrirOCrearAsync(CarpetaSeleccionada, CancellationToken.None, excluidos);
```
- Agregar helpers para test (al final de la clase):
```csharp
    internal void CargarCandidatosParaTest(string carpeta) => CargarArchivos(carpeta);
    internal Task AnalizarParaTestAsync() => Analizar();
```
(El método `Analizar` es `private async Task`; el helper lo invoca. Si `Analizar` referenciara `_nav` cuando es null, el test pasa `null!` y `Analizar` navega — para el test, hacé que la navegación tolere `_nav` null: envolver `_nav?.Navegar(...)`. Verificar que `Analizar` use `_nav?.` ; si hoy usa `_nav.Navegar`, cambiar a `_nav?.Navegar`.)

- [ ] **Step 4: Actualizar `VistaConfigurar.xaml`**

Reemplazar el `ItemsControl` de archivos (líneas ≈77-93) por uno con checkbox:

```xml
            <ItemsControl ItemsSource="{Binding Archivos}">
              <ItemsControl.ItemTemplate>
                <DataTemplate DataType="{x:Type vm:ArchivoSeleccionableVm}">
                  <Border Padding="8,5" Margin="0,2" CornerRadius="4"
                          Background="{DynamicResource ControlFillColorDefaultBrush}">
                    <CheckBox IsChecked="{Binding Incluido, Mode=TwoWay}"
                              VerticalContentAlignment="Center">
                      <StackPanel Orientation="Horizontal">
                        <ui:SymbolIcon Symbol="Document24" FontSize="14" Margin="0,0,8,0"
                                       Foreground="{DynamicResource SystemAccentColorBrush}"/>
                        <TextBlock Text="{Binding Nombre}" FontSize="12"
                                   VerticalAlignment="Center"
                                   Foreground="{DynamicResource TextFillColorPrimaryBrush}"/>
                      </StackPanel>
                    </CheckBox>
                  </Border>
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
```

Y agregar, bajo el título "Archivos detectados", una aclaración:
```xml
          <TextBlock Text="Destildá los que no quieras procesar."
                     FontSize="11"
                     Foreground="{DynamicResource TextFillColorTertiaryBrush}"
                     Margin="0,0,0,8"/>
```

- [ ] **Step 5: Correr los tests y la suite**

Run: `dotnet test tests/Resumenes.Ui.Tests -c Debug --filter ConfigurarVmTests`
Expected: PASS (2 tests).

Run: `dotnet test -c Debug`
Expected: verde.

- [ ] **Step 6: Commit**

```bash
git add src/Resumenes.Ui/ViewModels/ArchivoSeleccionableVm.cs \
        src/Resumenes.Ui/ViewModels/ConfigurarVm.cs \
        src/Resumenes.Ui/Vistas/VistaConfigurar.xaml \
        tests/Resumenes.Ui.Tests/ConfigurarVmTests.cs
git commit -m "feat(archivos): excluir archivos con checkboxes antes de procesar"
```

---

## Task 4: "Acerca de" + LinkedIn en Configuración

**Files:**
- Modify: `src/Resumenes.Ui/ViewModels/ConfiguracionVm.cs`
- Modify: `src/Resumenes.Ui/Vistas/VistaConfiguracion.xaml`

**Interfaces:**
- Produces: `ConfiguracionVm.VersionApp` (string), `ConfiguracionVm.AbrirLinkedInCommand`.

- [ ] **Step 1: Agregar versión y comando en `ConfiguracionVm`**

En `ConfiguracionVm.cs`, agregar:

```csharp
    /// <summary>Versión legible de la app, leída del ensamblado.</summary>
    public string VersionApp =>
        "v" + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0");

    [RelayCommand]
    private void AbrirLinkedIn()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://ar.linkedin.com/in/emmanuel-zelarayan/es",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MensajeEstado = $"No se pudo abrir el enlace: {ex.Message}";
        }
    }
```

- [ ] **Step 2: Agregar la sección "Acerca de" en `VistaConfiguracion.xaml`**

Insertar, antes del botón "Guardar configuración", una `ui:Card`:

```xml
      <!-- ── Acerca de ── -->
      <ui:Card Padding="20" Margin="0,0,0,16">
        <StackPanel>
          <TextBlock Text="Acerca de" FontSize="14" FontWeight="SemiBold"
                     Foreground="{DynamicResource TextFillColorPrimaryBrush}" Margin="0,0,0,8"/>
          <TextBlock FontSize="13" Foreground="{DynamicResource TextFillColorSecondaryBrush}">
            <Run Text="Resúmenes de Estudio"/>
            <Run Text="·"/>
            <Run Text="{Binding VersionApp, Mode=OneWay}"/>
          </TextBlock>
          <TextBlock Text="Desarrollado por Emmanuel Zelarayán" FontSize="12"
                     Foreground="{DynamicResource TextFillColorTertiaryBrush}" Margin="0,2,0,10"/>
          <ui:Button Content="Ver perfil de LinkedIn"
                     Command="{Binding AbrirLinkedInCommand}"
                     Icon="{ui:SymbolIcon Symbol=Person24}"
                     HorizontalAlignment="Left"/>
        </StackPanel>
      </ui:Card>
```

- [ ] **Step 3: Compilar y correr todo**

Run: `dotnet build Resumenes.sln -c Debug`
Expected: BUILD succeeded. Si `Person24` no existe, usar `Contact24` o `Link24`.

Run: `dotnet test -c Debug`
Expected: verde.

- [ ] **Step 4: Verificación manual (el usuario prueba la UI)**

Cerrar `Resumenes.Ui`. Ejecutar la app:
1. Nuevo análisis → elegir carpeta: aparecen los archivos de nivel superior con checkbox; destildar uno → "Analizar": ese archivo no se procesa.
2. Reabrir/continuar el mismo análisis: el archivo destildado sigue excluido.
3. Configuración → "Acerca de": muestra nombre + versión; "Ver perfil de LinkedIn" abre el navegador en el perfil.

- [ ] **Step 5: Commit**

```bash
git add src/Resumenes.Ui/ViewModels/ConfiguracionVm.cs src/Resumenes.Ui/Vistas/VistaConfiguracion.xaml
git commit -m "feat(about): seccion Acerca de con LinkedIn en Configuracion"
```

---

## Self-Review (cobertura de la spec, Fase 4)

- **Quitar/excluir archivos antes de procesar:** Task 3 (UI), Task 2 (servicio). ✅
- **Solo se procesan los incluidos; originales intactos:** Task 2 (filtro, sin borrar). ✅
- **La exclusión afecta el fingerprint (set incluido):** Task 2. ✅
- **Persistencia para reanudar sin duplicar análisis:** Task 1 + Task 2 (test de reanudación). ✅
- **Lista mostrada = lo que se procesa (nivel superior):** Task 2 (`ListarArchivosCandidatos`) + Task 3. ✅
- **LinkedIn en "Acerca de":** Task 4. ✅
- **Migración no destructiva (`schema_version`→5):** Task 1. ✅

## Notas de diseño

- Las exclusiones se indexan por `carpeta_origen` absoluta (estable), evitando el ciclo fingerprint↔id (el id lleva un GUID aleatorio, no es determinista).
- Se corrige la inconsistencia previa: `ConfigurarVm` ya no escanea recursivo por su cuenta; usa `ListarArchivosCandidatos` (nivel superior), que coincide con lo que el servicio procesa.
- La sección "Acerca de" muestra la versión del ensamblado (`GetName().Version`); si se quiere fijar a la versión de release, se puede leer de un recurso/constante en el futuro (backlog menor).
