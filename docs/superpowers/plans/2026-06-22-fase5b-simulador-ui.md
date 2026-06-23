# Fase 5b — Simulador de exámenes: UI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir la UI del simulador sobre el motor (5a): historial de exámenes por análisis, asistente de creación, rendición interactiva por tipo de pregunta (con timer y autoguardado) y pantalla de resultado con feedback.

**Architecture:** 4 páginas WPF (`VistaExamenes`, `VistaCrearExamen`, `VistaRendirExamen`, `VistaResultadoExamen`) con sus ViewModels, siguiendo el patrón existente: el VM es el `DataContext`; el parámetro de navegación se consume en `Loaded` vía `ServicioNavegacion.ConsumirParametro()`. La rendición usa `PreguntaRendirVm` (envuelve cada `PreguntaExamen`, parsea su `DatosJson` a controles bindeable y serializa la respuesta del usuario al contrato `RespuestaJson` que el motor espera). El motor (`IServicioExamenes`, `IRepositorioExamenes`) ya existe.

**Tech Stack:** .NET 9, WPF / WPF-UI 4.x, CommunityToolkit.Mvvm, xUnit.

## Global Constraints

- **Plataforma:** Windows-only; MVVM con CommunityToolkit (`[ObservableProperty]`, `[RelayCommand]`).
- **Patrón de navegación:** registrar Vista + VM en DI; navegar por tipo con `ServicioNavegacion.Navegar<TVista>(parametro)`; la vista consume el parámetro en `Loaded`.
- **Contrato `RespuestaJson` (lo que la UI debe producir, lo que el corrector lee):**
  - `McUna` → índice elegido como número, p. ej. `2`
  - `McVarias` → array de índices, p. ej. `[0,2]`
  - `Completar` → array de strings (uno por hueco), p. ej. `["fotosíntesis","clorofila"]`
  - `Emparejar` → array de pares `[i,j]`, p. ej. `[[0,1],[1,0]]`
  - `Desarrollo` → string JSON, p. ej. `"mi respuesta..."`
  - `DesarrolloItems` → array de strings (uno por ítem)
  - `VfJustificado` → objeto `{"vf":true,"justificacion":"..."}` (la IA evalúa ambos)
- **Corrección/nota:** las hace el motor (`FinalizarYCorregirAsync`); la UI solo recopila respuestas y muestra el resultado.
- **Autoguardado:** cada cambio de respuesta persiste vía `IRepositorioExamenes.GuardarRespuesta` (reanudable).
- **Timer:** cuenta regresiva; al expirar → autoentrega (corrige lo respondido).
- **Vistas XAML:** se validan manualmente (decisión del proyecto). Los ViewModels se testean con xUnit.
- **Build de verificación:** `dotnet build Resumenes.sln -c Debug` debe pasar.

## Decisiones a confirmar

1. **Selección de temas en el asistente:** en este MVP el examen usa **todos los temas** del análisis (`ConfigExamen.TemasIncluidos` vacío). La selección granular de temas queda como mejora (requiere listar temas con nombre legible; backlog). ¿OK?
2. **Punto de entrada:** un botón **"Exámenes"** en la pantalla de Resultados de un análisis terminado abre el historial de exámenes de ese análisis. (No se agrega ítem global en el menú lateral, porque los exámenes son por-análisis.)

---

## File Structure

- `src/Resumenes.Ui/ViewModels/ParametrosExamen.cs` — **Create**: records de navegación (`ParametroExamenes`, `ParametroCrearExamen`, `ParametroRendir`, `ParametroResultadoExamen`).
- `src/Resumenes.Ui/ViewModels/ExamenesVm.cs` + `Vistas/VistaExamenes.xaml(.cs)` — **Create**: historial.
- `src/Resumenes.Ui/ViewModels/CrearExamenVm.cs` + `Vistas/VistaCrearExamen.xaml(.cs)` — **Create**: asistente.
- `src/Resumenes.Ui/ViewModels/PreguntaRendirVm.cs` — **Create**: VM por pregunta (parseo + serialización).
- `src/Resumenes.Ui/ViewModels/RendirExamenVm.cs` + `Vistas/VistaRendirExamen.xaml(.cs)` — **Create**: rendición.
- `src/Resumenes.Ui/ViewModels/ResultadoExamenVm.cs` + `Vistas/VistaResultadoExamen.xaml(.cs)` — **Create**: resultado.
- `src/Resumenes.Ui/ViewModels/ResultadosVm.cs` + `Vistas/VistaResultados.xaml` — **Modify**: botón "Exámenes".
- `src/Resumenes.Ui/App.xaml.cs` — **Modify**: registrar vistas y VMs nuevos.
- Tests: `tests/Resumenes.Ui.Tests/ExamenesVmTests.cs`, `CrearExamenVmTests.cs`, `RendirExamenVmTests.cs`, `ResultadoExamenVmTests.cs`.

---

## Task 1: Historial de exámenes + entrada desde Resultados

**Files:**
- Create: `src/Resumenes.Ui/ViewModels/ParametrosExamen.cs`, `ExamenesVm.cs`, `Vistas/VistaExamenes.xaml`, `Vistas/VistaExamenes.xaml.cs`
- Modify: `src/Resumenes.Ui/ViewModels/ResultadosVm.cs`, `Vistas/VistaResultados.xaml`, `src/Resumenes.Ui/App.xaml.cs`
- Test: `tests/Resumenes.Ui.Tests/ExamenesVmTests.cs`

**Interfaces:**
- Consumes: `IServicioExamenes.Historial`, `IRepositorioExamenes.EliminarExamen`, `ServicioNavegacion`.
- Produces: `ParametroExamenes(Analisis An)`, `ParametroCrearExamen(Analisis An)`, `ParametroRendir(string ExamenId, Analisis An)`, `ParametroResultadoExamen(string ExamenId, Analisis An)`; `ExamenesVm` con `Cargar(Analisis)`, `ObservableCollection<ExamenItemVm> Examenes`, comandos `NuevoExamen`/`Rendir`/`VerResultado`/`Eliminar`.

- [ ] **Step 1: Escribir el test que falla**

Crear `tests/Resumenes.Ui.Tests/ExamenesVmTests.cs`. Usa un fake de `IServicioExamenes` que devuelve un historial.

```csharp
using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;
using Resumenes.Ui.ViewModels;
using Xunit;

namespace Resumenes.Ui.Tests;

public class ExamenesVmTests
{
    private sealed class ServicioExamenesFake : IServicioExamenes
    {
        public List<Examen> Lista = new();
        public Task<Examen> CrearAsync(string a, string t, ConfigExamen c, CancellationToken ct) => throw new NotImplementedException();
        public Task<Examen> FinalizarYCorregirAsync(string id, CancellationToken ct) => throw new NotImplementedException();
        public IReadOnlyList<Examen> Historial(string analisisId) => Lista;
    }

    private static Analisis An() => new("an1","n","c","fp",EstadoAnalisis.Completado,DateTime.UtcNow,DateTime.UtcNow);

    [Fact]
    public void Cargar_PoblaHistorial()
    {
        var svc = new ServicioExamenesFake();
        svc.Lista.Add(new Examen { Id="e1", AnalisisId="an1", Titulo="Parcial", Estado=EstadoExamen.Corregido,
            Nota=8, Porcentaje=80, Aprobado=true, CreadoEn=DateTime.UtcNow });
        var vm = new ExamenesVm(svc, null!, null!);

        vm.Cargar(An());

        Assert.Single(vm.Examenes);
        Assert.Equal("Parcial", vm.Examenes[0].Titulo);
        Assert.True(vm.Examenes[0].EstaCorregido);
        Assert.Contains("8", vm.Examenes[0].NotaLegible);
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test tests/Resumenes.Ui.Tests -c Debug --filter ExamenesVmTests`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear los parámetros de navegación**

Crear `src/Resumenes.Ui/ViewModels/ParametrosExamen.cs`:
```csharp
using Resumenes.Core.Modelos;

namespace Resumenes.Ui.ViewModels;

public record ParametroExamenes(Analisis An);
public record ParametroCrearExamen(Analisis An);
public record ParametroRendir(string ExamenId, Analisis An);
public record ParametroResultadoExamen(string ExamenId, Analisis An);
```

- [ ] **Step 4: Crear `ExamenesVm`**

Crear `src/Resumenes.Ui/ViewModels/ExamenesVm.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;
using Resumenes.Ui.Servicios;
using Resumenes.Ui.Vistas;

namespace Resumenes.Ui.ViewModels;

public partial class ExamenesVm : VistaModeloBase
{
    private readonly IServicioExamenes _svc;
    private readonly IRepositorioExamenes _repo;
    private readonly ServicioNavegacion _nav;
    private Analisis? _an;

    [ObservableProperty] private ObservableCollection<ExamenItemVm> _examenes = new();

    public ExamenesVm(IServicioExamenes svc, IRepositorioExamenes repo, ServicioNavegacion nav)
    {
        _svc = svc; _repo = repo; _nav = nav;
    }

    public void Cargar(Analisis an)
    {
        _an = an;
        Examenes = new ObservableCollection<ExamenItemVm>(
            _svc.Historial(an.Id).Select(e => new ExamenItemVm(e)));
    }

    [RelayCommand]
    private void NuevoExamen()
    {
        if (_an is not null) _nav.Navegar<VistaCrearExamen>(new ParametroCrearExamen(_an));
    }

    [RelayCommand]
    private void Rendir(ExamenItemVm? item)
    {
        if (item is null || _an is null) return;
        _nav.Navegar<VistaRendirExamen>(new ParametroRendir(item.Id, _an));
    }

    [RelayCommand]
    private void VerResultado(ExamenItemVm? item)
    {
        if (item is null || _an is null) return;
        _nav.Navegar<VistaResultadoExamen>(new ParametroResultadoExamen(item.Id, _an));
    }

    [RelayCommand]
    private void Eliminar(ExamenItemVm? item)
    {
        if (item is null) return;
        _repo.EliminarExamen(item.Id);
        Examenes.Remove(item);
    }
}

public sealed class ExamenItemVm
{
    private readonly Examen _e;
    public ExamenItemVm(Examen e) => _e = e;
    public string Id => _e.Id;
    public string Titulo => _e.Titulo;
    public string FechaLegible => _e.CreadoEn.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public bool EstaCorregido => _e.Estado == EstadoExamen.Corregido;
    public bool EnCurso => _e.Estado is EstadoExamen.Borrador or EstadoExamen.EnCurso;
    public string NotaLegible => _e.Nota is null ? "—"
        : $"Nota {_e.Nota:0.#} ({_e.Porcentaje:0}%)" + (_e.Aprobado == true ? " ✓" : "");
}
```

- [ ] **Step 5: Crear la vista `VistaExamenes`**

Crear `src/Resumenes.Ui/Vistas/VistaExamenes.xaml` (lista de tarjetas con botones Rendir/Ver resultado/Eliminar y un botón "Nuevo examen"). Estructura análoga a `VistaInicio.xaml` (ItemsControl + DataTemplate con `ui:Card`, botones con `Command="{Binding DataContext.XCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}" CommandParameter="{Binding}"`). Mostrar `Titulo`, `FechaLegible`, `NotaLegible`; botón "Rendir" visible si `EnCurso`, "Ver resultado" si `EstaCorregido`. Incluir un encabezado con el botón `NuevoExamenCommand`.

Crear `src/Resumenes.Ui/Vistas/VistaExamenes.xaml.cs`:
```csharp
using System.Windows;
using System.Windows.Controls;
using Resumenes.Ui.Servicios;
using Resumenes.Ui.ViewModels;

namespace Resumenes.Ui.Vistas;

public partial class VistaExamenes : Page
{
    private readonly ExamenesVm _vm;
    private readonly ServicioNavegacion _nav;
    public VistaExamenes(ExamenesVm vm, ServicioNavegacion nav)
    {
        _vm = vm; _nav = nav;
        InitializeComponent();
        DataContext = vm;
        Loaded += OnCargado;
    }
    private void OnCargado(object sender, RoutedEventArgs e)
    {
        Loaded -= OnCargado;
        if (_nav.ConsumirParametro() is ParametroExamenes p) _vm.Cargar(p.An);
    }
}
```

- [ ] **Step 6: Botón "Exámenes" en Resultados**

En `src/Resumenes.Ui/ViewModels/ResultadosVm.cs`: agregar campo `_nav` ya existe; agregar comando:
```csharp
    [RelayCommand]
    private void IrAExamenes()
    {
        if (_analisis is not null) _nav.Navegar<VistaExamenes>(new ParametroExamenes(_analisis));
    }
```
(`_analisis` ya es un campo de `ResultadosVm`, seteado en `Cargar`.)

En `src/Resumenes.Ui/Vistas/VistaResultados.xaml`: agregar un botón cerca del encabezado:
```xml
      <ui:Button Content="Simular examen"
                 Command="{Binding IrAExamenesCommand}"
                 Icon="{ui:SymbolIcon Symbol=Notebook24}"
                 Appearance="Primary"
                 Margin="0,0,0,12" HorizontalAlignment="Left"/>
```

- [ ] **Step 7: Registrar en DI**

En `src/Resumenes.Ui/App.xaml.cs`, registrar (junto a las demás vistas/VMs):
```csharp
        sc.AddTransient<ExamenesVm>(sp => new ExamenesVm(
            sp.GetRequiredService<Resumenes.Core.Interfaces.IServicioExamenes>(),
            sp.GetRequiredService<Resumenes.Core.Interfaces.IRepositorioExamenes>(),
            sp.GetRequiredService<ServicioNavegacion>()));
        sc.AddTransient<VistaExamenes>();
```
(Las VMs/Vistas de las tasks 2-4 se registran en sus tasks.)

- [ ] **Step 8: Correr el test y la suite**

Run: `dotnet test tests/Resumenes.Ui.Tests -c Debug --filter ExamenesVmTests`
Expected: PASS.

Run: `dotnet build Resumenes.sln -c Debug`
Expected: BUILD succeeded. (Las navegaciones a `VistaCrearExamen`/`VistaRendirExamen`/`VistaResultadoExamen` referencian tipos que se crean en las tasks 2-4. Para que compile, crear en esta task **stubs mínimos** de esas 3 páginas vacías —`Page` con su `.xaml`/`.xaml.cs` vacío— o, mejor, comentar temporalmente esas navegaciones y completarlas en sus tasks. **Decisión:** crear las 3 vistas como stubs vacíos en sus respectivas tasks ANTES de referenciarlas; para esta task, dejar los comandos `Rendir`/`VerResultado` navegando a `VistaResultadoExamen`/`VistaRendirExamen` que se crean en tasks 3-4 → por lo tanto, en esta task, crear stubs vacíos de `VistaRendirExamen` y `VistaResultadoExamen` y `VistaCrearExamen` —un `Page` vacío cada uno— y reemplazar su contenido en las tasks siguientes.)

Crear stubs vacíos (solo para compilar): `VistaCrearExamen.xaml/.cs`, `VistaRendirExamen.xaml/.cs`, `VistaResultadoExamen.xaml/.cs` como `Page` vacíos con constructor `public VistaX() { InitializeComponent(); }`. Las tasks 2-4 los reemplazan con el contenido real (y agregan sus VMs + registro DI).

- [ ] **Step 9: Commit**

```bash
git add src/Resumenes.Ui/ViewModels/ParametrosExamen.cs src/Resumenes.Ui/ViewModels/ExamenesVm.cs \
        src/Resumenes.Ui/Vistas/VistaExamenes.xaml src/Resumenes.Ui/Vistas/VistaExamenes.xaml.cs \
        src/Resumenes.Ui/Vistas/VistaCrearExamen.xaml src/Resumenes.Ui/Vistas/VistaCrearExamen.xaml.cs \
        src/Resumenes.Ui/Vistas/VistaRendirExamen.xaml src/Resumenes.Ui/Vistas/VistaRendirExamen.xaml.cs \
        src/Resumenes.Ui/Vistas/VistaResultadoExamen.xaml src/Resumenes.Ui/Vistas/VistaResultadoExamen.xaml.cs \
        src/Resumenes.Ui/ViewModels/ResultadosVm.cs src/Resumenes.Ui/Vistas/VistaResultados.xaml \
        src/Resumenes.Ui/App.xaml.cs tests/Resumenes.Ui.Tests/ExamenesVmTests.cs
git commit -m "feat(examenes-ui): historial de examenes + entrada desde resultados (+ stubs de vistas)"
```

---

## Task 2: Asistente de creación de examen

**Files:**
- Create: `src/Resumenes.Ui/ViewModels/CrearExamenVm.cs`
- Modify: `src/Resumenes.Ui/Vistas/VistaCrearExamen.xaml(.cs)` (reemplazar el stub), `src/Resumenes.Ui/App.xaml.cs`
- Test: `tests/Resumenes.Ui.Tests/CrearExamenVmTests.cs`

**Interfaces:**
- Consumes: `IServicioExamenes.CrearAsync`, `ServicioNavegacion`.
- Produces: `CrearExamenVm` con props de configuración (cantidades por tipo, dificultad, puntos, tiempo, fuente), comando `Crear` que arma `ConfigExamen` y navega a `VistaRendirExamen`.

- [ ] **Step 1: Escribir el test que falla**

Crear `tests/Resumenes.Ui.Tests/CrearExamenVmTests.cs`. Verifica que `Crear` arma el `ConfigExamen` con las cantidades elegidas y llama a `CrearAsync`.

```csharp
using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;
using Resumenes.Ui.ViewModels;
using Xunit;

namespace Resumenes.Ui.Tests;

public class CrearExamenVmTests
{
    private sealed class SvcFake : IServicioExamenes
    {
        public ConfigExamen? CfgRecibida;
        public Task<Examen> CrearAsync(string a, string t, ConfigExamen c, CancellationToken ct)
        { CfgRecibida = c; return Task.FromResult(new Examen { Id="e1", AnalisisId=a, Titulo=t, CreadoEn=DateTime.UtcNow }); }
        public Task<Examen> FinalizarYCorregirAsync(string id, CancellationToken ct) => throw new NotImplementedException();
        public IReadOnlyList<Examen> Historial(string analisisId) => Array.Empty<Examen>();
    }
    private static Analisis An() => new("an1","n","c","fp",EstadoAnalisis.Completado,DateTime.UtcNow,DateTime.UtcNow);

    [Fact]
    public async Task Crear_ArmaConfigYLlamaServicio()
    {
        var svc = new SvcFake();
        var vm = new CrearExamenVm(svc, null!);
        vm.Cargar(An());
        vm.CantidadMcUna = 3;
        vm.CantidadDesarrollo = 1;
        vm.PuntosTotales = 10;
        vm.TiempoLimiteMin = 20;
        vm.FuenteRapida = true;

        await vm.CrearParaTestAsync();

        Assert.NotNull(svc.CfgRecibida);
        Assert.Equal("rapido", svc.CfgRecibida!.Fuente);
        Assert.Contains(svc.CfgRecibida.Tipos, t => t.Tipo == TipoPregunta.McUna && t.Cantidad == 3);
        Assert.Contains(svc.CfgRecibida.Tipos, t => t.Tipo == TipoPregunta.Desarrollo && t.Cantidad == 1);
        Assert.DoesNotContain(svc.CfgRecibida.Tipos, t => t.Cantidad == 0);  // no incluir tipos en 0
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test tests/Resumenes.Ui.Tests -c Debug --filter CrearExamenVmTests`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `CrearExamenVm`**

Crear `src/Resumenes.Ui/ViewModels/CrearExamenVm.cs`. Una propiedad de cantidad por cada uno de los 7 tipos, + dificultad/puntos/tiempo/fuente. `Crear` arma `ConfigExamen` (omitiendo tipos en 0), llama `CrearAsync` y navega a rendir.

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;
using Resumenes.Ui.Servicios;
using Resumenes.Ui.Vistas;

namespace Resumenes.Ui.ViewModels;

public partial class CrearExamenVm : VistaModeloBase
{
    private readonly IServicioExamenes _svc;
    private readonly ServicioNavegacion _nav;
    private Analisis? _an;

    [ObservableProperty] private string _titulo = "Examen";
    [ObservableProperty] private int _cantidadMcUna = 5;
    [ObservableProperty] private int _cantidadMcVarias;
    [ObservableProperty] private int _cantidadVf;
    [ObservableProperty] private int _cantidadDesarrollo;
    [ObservableProperty] private int _cantidadDesarrolloItems;
    [ObservableProperty] private int _cantidadCompletar;
    [ObservableProperty] private int _cantidadEmparejar;
    [ObservableProperty] private string _dificultad = "media";   // facil|media|dificil
    [ObservableProperty] private double _puntosTotales = 10;
    [ObservableProperty] private int _tiempoLimiteMin = 30;
    [ObservableProperty] private bool _fuenteRapida = true;      // true=rapido(resúmenes) / false=completo
    [ObservableProperty] private bool _generando;
    [ObservableProperty] private string _mensajeError = string.Empty;

    public CrearExamenVm(IServicioExamenes svc, ServicioNavegacion nav) { _svc = svc; _nav = nav; }

    public void Cargar(Analisis an) => _an = an;

    [RelayCommand]
    private async Task Crear()
    {
        if (_an is null) return;
        var tipos = new List<CantidadPorTipo>
        {
            new(TipoPregunta.McUna, CantidadMcUna),
            new(TipoPregunta.McVarias, CantidadMcVarias),
            new(TipoPregunta.VfJustificado, CantidadVf),
            new(TipoPregunta.Desarrollo, CantidadDesarrollo),
            new(TipoPregunta.DesarrolloItems, CantidadDesarrolloItems),
            new(TipoPregunta.Completar, CantidadCompletar),
            new(TipoPregunta.Emparejar, CantidadEmparejar),
        }.Where(t => t.Cantidad > 0).ToList();

        if (tipos.Count == 0) { MensajeError = "Elegí al menos una pregunta."; return; }

        Generando = true; MensajeError = string.Empty;
        try
        {
            var cfg = new ConfigExamen(tipos, System.Array.Empty<string>(), Dificultad,
                PuntosTotales, TiempoLimiteMin, FuenteRapida ? "rapido" : "completo");
            var examen = await _svc.CrearAsync(_an.Id, Titulo.Trim(), cfg, System.Threading.CancellationToken.None);
            _nav.Navegar<VistaRendirExamen>(new ParametroRendir(examen.Id, _an));
        }
        catch (System.Exception ex) { MensajeError = $"No se pudo crear el examen: {ex.Message}"; }
        finally { Generando = false; }
    }

    internal Task CrearParaTestAsync() => Crear();
}
```

- [ ] **Step 4: Reemplazar el stub `VistaCrearExamen`**

Reemplazar `src/Resumenes.Ui/Vistas/VistaCrearExamen.xaml` por una `Page` con: campo Título, `ui:NumberBox` por cada tipo (7), un `ComboBox` de dificultad (fácil/media/difícil), `NumberBox` de puntos y de tiempo, un par de `RadioButton` (rápido/completo), un `TextBlock` de error y el botón "Crear examen" (`CrearCommand`). El `.xaml.cs` sigue el patrón Loaded:
```csharp
private void OnCargado(object sender, RoutedEventArgs e)
{
    Loaded -= OnCargado;
    if (_nav.ConsumirParametro() is ParametroCrearExamen p) _vm.Cargar(p.An);
}
```
(constructor `VistaCrearExamen(CrearExamenVm vm, ServicioNavegacion nav)` con `DataContext = vm`.)

- [ ] **Step 5: Registrar en DI**

En `App.xaml.cs`:
```csharp
        sc.AddTransient<CrearExamenVm>(sp => new CrearExamenVm(
            sp.GetRequiredService<Resumenes.Core.Interfaces.IServicioExamenes>(),
            sp.GetRequiredService<ServicioNavegacion>()));
        // VistaCrearExamen ya está registrada (stub) en Task 1; si no, agregar: sc.AddTransient<VistaCrearExamen>();
```
(Confirmar que `sc.AddTransient<VistaCrearExamen>();` esté registrado; si el stub no lo registró, agregarlo.)

- [ ] **Step 6: Correr el test, la suite y el build**

Run: `dotnet test tests/Resumenes.Ui.Tests -c Debug --filter CrearExamenVmTests`  → PASS.
Run: `dotnet build Resumenes.sln -c Debug`  → BUILD succeeded.

- [ ] **Step 7: Commit**

```bash
git add src/Resumenes.Ui/ViewModels/CrearExamenVm.cs \
        src/Resumenes.Ui/Vistas/VistaCrearExamen.xaml src/Resumenes.Ui/Vistas/VistaCrearExamen.xaml.cs \
        src/Resumenes.Ui/App.xaml.cs tests/Resumenes.Ui.Tests/CrearExamenVmTests.cs
git commit -m "feat(examenes-ui): asistente de creacion de examen"
```

---

## Task 3: Rendición interactiva (timer + autoguardado)

**Files:**
- Create: `src/Resumenes.Ui/ViewModels/PreguntaRendirVm.cs`, `RendirExamenVm.cs`
- Modify: `src/Resumenes.Ui/Vistas/VistaRendirExamen.xaml(.cs)` (reemplazar stub), `src/Resumenes.Ui/App.xaml.cs`
- Test: `tests/Resumenes.Ui.Tests/RendirExamenVmTests.cs`

**Interfaces:**
- Consumes: `IRepositorioExamenes` (ListarPreguntas, GuardarRespuesta, ListarRespuestas), `IServicioExamenes.FinalizarYCorregirAsync`, `ServicioNavegacion`.
- Produces: `PreguntaRendirVm` (parsea `DatosJson`, expone controles bindeable por tipo, `string ConstruirRespuestaJson()`, `bool? Vf`, etc.); `RendirExamenVm` con `Cargar(examenId, an)`, `ObservableCollection<PreguntaRendirVm> Preguntas`, navegación entre preguntas, `GuardarActual()` (autoguardado), `EntregarAsync()`.

- [ ] **Step 1: Escribir el test que falla**

Crear `tests/Resumenes.Ui.Tests/RendirExamenVmTests.cs`. Verifica que `PreguntaRendirVm` construye el `RespuestaJson` correcto por tipo, y que `EntregarAsync` persiste respuestas y llama a `FinalizarYCorregirAsync`.

```csharp
using Resumenes.Core.Modelos;
using Resumenes.Ui.ViewModels;
using Xunit;

namespace Resumenes.Ui.Tests;

public class RendirExamenVmTests
{
    [Fact]
    public void PreguntaRendirVm_McUna_SerializaIndice()
    {
        var p = new PreguntaExamen { Id="p", ExamenId="e", Tipo=TipoPregunta.McUna, Enunciado="?", Puntos=1,
            DatosJson="{\"opciones\":[{\"texto\":\"A\"},{\"texto\":\"B\"}]}" };
        var vm = new PreguntaRendirVm(p);
        vm.Opciones[1].Seleccionada = true;   // elige B (índice 1)
        Assert.Equal("1", vm.ConstruirRespuestaJson());
    }

    [Fact]
    public void PreguntaRendirVm_Completar_SerializaArray()
    {
        var p = new PreguntaExamen { Id="p", ExamenId="e", Tipo=TipoPregunta.Completar, Enunciado="?", Puntos=1,
            DatosJson="{\"texto\":\"a ___ b ___\",\"respuestas\":[\"x\",\"y\"]}" };
        var vm = new PreguntaRendirVm(p);
        Assert.Equal(2, vm.Huecos.Count);
        vm.Huecos[0].Valor = "uno"; vm.Huecos[1].Valor = "dos";
        Assert.Equal("[\"uno\",\"dos\"]", vm.ConstruirRespuestaJson());
    }

    [Fact]
    public void PreguntaRendirVm_Vf_SerializaObjeto()
    {
        var p = new PreguntaExamen { Id="p", ExamenId="e", Tipo=TipoPregunta.VfJustificado, Enunciado="?", Puntos=1,
            DatosJson="{\"afirmacion\":\"el sol es una estrella\"}" };
        var vm = new PreguntaRendirVm(p);
        vm.Vf = true; vm.TextoRespuesta = "porque emite luz propia";
        var json = vm.ConstruirRespuestaJson();
        Assert.Contains("\"vf\":true", json);
        Assert.Contains("porque emite luz propia", json);
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test tests/Resumenes.Ui.Tests -c Debug --filter RendirExamenVmTests`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `PreguntaRendirVm`**

Crear `src/Resumenes.Ui/ViewModels/PreguntaRendirVm.cs`. Parsea `DatosJson` según `Tipo` y expone lo necesario para binding + `ConstruirRespuestaJson()`:

```csharp
using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Resumenes.Core.Modelos;

namespace Resumenes.Ui.ViewModels;

public partial class OpcionRendirVm : ObservableObject
{
    public required string Texto { get; init; }
    [ObservableProperty] private bool _seleccionada;
}
public partial class HuecoVm : ObservableObject
{
    [ObservableProperty] private string _valor = "";
}
public partial class ItemDesarrolloVm : ObservableObject
{
    public required string Enunciado { get; init; }
    [ObservableProperty] private string _texto = "";
}

public partial class PreguntaRendirVm : ObservableObject
{
    public PreguntaExamen Pregunta { get; }
    public TipoPregunta Tipo => Pregunta.Tipo;
    public string Enunciado => Pregunta.Enunciado;
    public double Puntos => Pregunta.Puntos;

    public ObservableCollection<OpcionRendirVm> Opciones { get; } = new();
    public ObservableCollection<HuecoVm> Huecos { get; } = new();
    public ObservableCollection<ItemDesarrolloVm> Items { get; } = new();
    public ObservableCollection<string> Izquierda { get; } = new();   // Emparejar
    public ObservableCollection<string> Derecha { get; } = new();
    public string? Afirmacion { get; }

    [ObservableProperty] private bool? _vf;            // VfJustificado (parte V/F)
    [ObservableProperty] private string _textoRespuesta = "";  // Desarrollo / justificación
    [ObservableProperty] private bool _marcadaParaRevisar;
    // Para Emparejar: índice de la derecha elegido por cada izquierda (paralelo a Izquierda)
    public ObservableCollection<int> SeleccionEmparejar { get; } = new();

    public PreguntaRendirVm(PreguntaExamen p)
    {
        Pregunta = p;
        using var d = JsonDocument.Parse(p.DatosJson);
        var root = d.RootElement;
        switch (p.Tipo)
        {
            case TipoPregunta.McUna:
            case TipoPregunta.McVarias:
                foreach (var o in root.GetProperty("opciones").EnumerateArray())
                    Opciones.Add(new OpcionRendirVm { Texto = o.GetProperty("texto").GetString() ?? "" });
                break;
            case TipoPregunta.Completar:
                foreach (var _ in root.GetProperty("respuestas").EnumerateArray()) Huecos.Add(new HuecoVm());
                break;
            case TipoPregunta.DesarrolloItems:
                foreach (var it in root.GetProperty("items").EnumerateArray())
                    Items.Add(new ItemDesarrolloVm { Enunciado = it.GetProperty("enunciado").GetString() ?? "" });
                break;
            case TipoPregunta.Emparejar:
                foreach (var x in root.GetProperty("izquierda").EnumerateArray()) { Izquierda.Add(x.GetString() ?? ""); SeleccionEmparejar.Add(-1); }
                foreach (var y in root.GetProperty("derecha").EnumerateArray()) Derecha.Add(y.GetString() ?? "");
                break;
            case TipoPregunta.VfJustificado:
                Afirmacion = root.TryGetProperty("afirmacion", out var af) ? af.GetString() : p.Enunciado;
                break;
        }
    }

    public string ConstruirRespuestaJson() => Tipo switch
    {
        TipoPregunta.McUna => SerializarMcUna(),
        TipoPregunta.McVarias => JsonSerializer.Serialize(Indices()),
        TipoPregunta.Completar => JsonSerializer.Serialize(Huecos.Select(h => h.Valor)),
        TipoPregunta.Emparejar => JsonSerializer.Serialize(
            SeleccionEmparejar.Select((j, i) => new[] { i, j }).Where(p => p[1] >= 0)),
        TipoPregunta.DesarrolloItems => JsonSerializer.Serialize(Items.Select(i => i.Texto)),
        TipoPregunta.VfJustificado => JsonSerializer.Serialize(new { vf = Vf ?? false, justificacion = TextoRespuesta }),
        _ => JsonSerializer.Serialize(TextoRespuesta),   // Desarrollo
    };

    private List<int> Indices() => Opciones.Select((o, i) => (o, i)).Where(x => x.o.Seleccionada).Select(x => x.i).ToList();
    private string SerializarMcUna()
    {
        var idx = Indices();
        return idx.Count > 0 ? idx[0].ToString() : "null";
    }
}
```

- [ ] **Step 4: Crear `RendirExamenVm`**

Crear `src/Resumenes.Ui/ViewModels/RendirExamenVm.cs`. Carga las preguntas, mantiene índice actual, navega, autoguarda la respuesta actual (persistiendo `RespuestaUsuario` con el JSON construido), timer con `DispatcherTimer`, y `EntregarAsync` que guarda todo y llama `FinalizarYCorregirAsync` y navega al resultado.

```csharp
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;
using Resumenes.Ui.Servicios;
using Resumenes.Ui.Vistas;

namespace Resumenes.Ui.ViewModels;

public partial class RendirExamenVm : VistaModeloBase
{
    private readonly IRepositorioExamenes _repo;
    private readonly IServicioExamenes _svc;
    private readonly ServicioNavegacion? _nav;
    private readonly DispatcherTimer _timer;
    private Analisis? _an;
    private string _examenId = "";
    private int _segundosRestantes;

    public ObservableCollection<PreguntaRendirVm> Preguntas { get; } = new();
    [ObservableProperty] private int _indiceActual;
    [ObservableProperty] private PreguntaRendirVm? _actual;
    [ObservableProperty] private string _textoTiempo = "";
    [ObservableProperty] private bool _entregando;

    public RendirExamenVm(IRepositorioExamenes repo, IServicioExamenes svc, ServicioNavegacion? nav = null)
    {
        _repo = repo; _svc = svc; _nav = nav;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = System.TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tictac();
    }

    public void Cargar(string examenId, Analisis an, int tiempoLimiteMin)
    {
        _examenId = examenId; _an = an;
        Preguntas.Clear();
        foreach (var p in _repo.ListarPreguntas(examenId)) Preguntas.Add(new PreguntaRendirVm(p));
        IndiceActual = 0;
        Actual = Preguntas.Count > 0 ? Preguntas[0] : null;
        _segundosRestantes = tiempoLimiteMin * 60;
        ActualizarTiempo();
        if (tiempoLimiteMin > 0) _timer.Start();
    }

    private void Tictac()
    {
        _segundosRestantes--;
        ActualizarTiempo();
        if (_segundosRestantes <= 0) { _timer.Stop(); _ = EntregarAsync(); }
    }

    private void ActualizarTiempo()
    {
        var t = System.TimeSpan.FromSeconds(System.Math.Max(0, _segundosRestantes));
        TextoTiempo = t.ToString(@"mm\:ss");
    }

    [RelayCommand] private void Siguiente() { GuardarActual(); if (IndiceActual < Preguntas.Count - 1) Actual = Preguntas[++IndiceActual]; }
    [RelayCommand] private void Anterior() { GuardarActual(); if (IndiceActual > 0) Actual = Preguntas[--IndiceActual]; }

    /// <summary>Persiste la respuesta de la pregunta actual (autoguardado).</summary>
    public void GuardarActual()
    {
        if (Actual is null) return;
        _repo.GuardarRespuesta(new RespuestaUsuario {
            Id = $"{_examenId}:{Actual.Pregunta.Id}", ExamenId = _examenId, PreguntaId = Actual.Pregunta.Id,
            RespuestaJson = Actual.ConstruirRespuestaJson() });
    }

    [RelayCommand]
    public async Task EntregarAsync()
    {
        if (Entregando) return;
        Entregando = true;
        _timer.Stop();
        foreach (var pr in Preguntas)
            _repo.GuardarRespuesta(new RespuestaUsuario {
                Id = $"{_examenId}:{pr.Pregunta.Id}", ExamenId = _examenId, PreguntaId = pr.Pregunta.Id,
                RespuestaJson = pr.ConstruirRespuestaJson() });
        await _svc.FinalizarYCorregirAsync(_examenId, System.Threading.CancellationToken.None);
        if (_an is not null) _nav?.Navegar<VistaResultadoExamen>(new ParametroResultadoExamen(_examenId, _an));
        Entregando = false;
    }
}
```

(Nota: el `Id` de `RespuestaUsuario` es determinista `examenId:preguntaId`, así el autoguardado y la entrega hacen upsert de la MISMA fila — no duplican.)

- [ ] **Step 5: Reemplazar el stub `VistaRendirExamen`**

Reemplazar `VistaRendirExamen.xaml` por una `Page` con: encabezado con `TextoTiempo` y progreso (pregunta X de N); un área que muestra `Actual` con un **DataTemplateSelector o DataTriggers por `Tipo`** que renderiza:
- McUna → `ItemsControl` de `RadioButton` (binding a `Seleccionada`, GroupName por pregunta).
- McVarias → `ItemsControl` de `CheckBox` (`Seleccionada`).
- VfJustificado → dos `RadioButton` (Verdadero/Falso → `Vf`) + `TextBox` multilínea (`TextoRespuesta`).
- Desarrollo → `TextBox` multilínea (`TextoRespuesta`).
- DesarrolloItems → `ItemsControl` de `Items` con `Enunciado` + `TextBox` (`Texto`).
- Completar → `ItemsControl` de `Huecos` con `TextBox` (`Valor`).
- Emparejar → `ItemsControl` de `Izquierda` con un `ComboBox` (ItemsSource=`Derecha`, SelectedIndex bindeado al elemento de `SeleccionEmparejar`).
- Botones: Anterior / Siguiente / "Marcar para revisar" (`MarcadaParaRevisar`) / "Entregar" (`EntregarCommand`).

`.xaml.cs`: patrón Loaded; recibir el tiempo límite leyendo el `ConfigJson` del examen (el VM puede leerlo del repo) — para simplificar, en `Cargar` el VM obtiene el examen del repo y deserializa `ConfigJson` para el `tiempoLimiteMin`. Ajustar `Cargar(examenId, an)` para leer el tiempo internamente:
```csharp
public void Cargar(string examenId, Analisis an)
{
    var examen = _repo.ObtenerExamen(examenId);
    int tiempo = 0;
    try { if (examen is not null) tiempo = System.Text.Json.JsonSerializer.Deserialize<ConfigExamen>(examen.ConfigJson)?.TiempoLimiteMin ?? 0; } catch { }
    CargarInterno(examenId, an, tiempo);
}
```
(renombrar el `Cargar(examenId, an, tiempoLimiteMin)` anterior a `CargarInterno`, y el test llama a `CargarInterno` o a un overload. Mantener un overload público `Cargar(examenId, an, tiempo)` para test.)

- [ ] **Step 6: Registrar en DI**

En `App.xaml.cs`:
```csharp
        sc.AddTransient<RendirExamenVm>(sp => new RendirExamenVm(
            sp.GetRequiredService<Resumenes.Core.Interfaces.IRepositorioExamenes>(),
            sp.GetRequiredService<Resumenes.Core.Interfaces.IServicioExamenes>(),
            sp.GetRequiredService<ServicioNavegacion>()));
        // VistaRendirExamen ya registrada (stub) en Task 1; confirmar sc.AddTransient<VistaRendirExamen>();
```

- [ ] **Step 7: Correr tests, suite y build**

Run: `dotnet test tests/Resumenes.Ui.Tests -c Debug --filter RendirExamenVmTests`  → PASS (3 tests).
Run: `dotnet test -c Debug`  → verde.
Run: `dotnet build Resumenes.sln -c Debug`  → BUILD succeeded.

- [ ] **Step 8: Commit**

```bash
git add src/Resumenes.Ui/ViewModels/PreguntaRendirVm.cs src/Resumenes.Ui/ViewModels/RendirExamenVm.cs \
        src/Resumenes.Ui/Vistas/VistaRendirExamen.xaml src/Resumenes.Ui/Vistas/VistaRendirExamen.xaml.cs \
        src/Resumenes.Ui/App.xaml.cs tests/Resumenes.Ui.Tests/RendirExamenVmTests.cs
git commit -m "feat(examenes-ui): rendicion interactiva por tipo con timer y autoguardado"
```

---

## Task 4: Pantalla de resultado

**Files:**
- Create: `src/Resumenes.Ui/ViewModels/ResultadoExamenVm.cs`
- Modify: `src/Resumenes.Ui/Vistas/VistaResultadoExamen.xaml(.cs)` (reemplazar stub), `src/Resumenes.Ui/App.xaml.cs`
- Test: `tests/Resumenes.Ui.Tests/ResultadoExamenVmTests.cs`

**Interfaces:**
- Consumes: `IRepositorioExamenes` (ObtenerExamen, ListarPreguntas, ListarRespuestas), `IServicioExamenes` (reintentar = crear otro con la misma config), `ServicioNavegacion`.
- Produces: `ResultadoExamenVm` con `Cargar(examenId, an)`, props `NotaLegible`/`PorcentajeLegible`/`Aprobado`/`FeedbackGeneral`, `ObservableCollection<ItemResultadoVm> Detalle`, comando `Reintentar`.

- [ ] **Step 1: Escribir el test que falla**

Crear `tests/Resumenes.Ui.Tests/ResultadoExamenVmTests.cs`. Usa `RepositorioExamenesEnMemoria` (del proyecto de tests `Resumenes.Tests`; agregar ProjectReference si falta — ya existe de fases previas) con un examen corregido + preguntas + respuestas.

```csharp
using Resumenes.Core.Modelos;
using Resumenes.Tests.Fakes;
using Resumenes.Ui.ViewModels;
using Xunit;

namespace Resumenes.Ui.Tests;

public class ResultadoExamenVmTests
{
    private static Analisis An() => new("an1","n","c","fp",EstadoAnalisis.Completado,DateTime.UtcNow,DateTime.UtcNow);

    [Fact]
    public void Cargar_MuestraNotaYDetalle()
    {
        var repo = new RepositorioExamenesEnMemoria();
        repo.GuardarExamen(new Examen { Id="e1", AnalisisId="an1", Titulo="P", Estado=EstadoExamen.Corregido,
            Nota=7, Porcentaje=70, Aprobado=true, FeedbackGeneral="Bien", CreadoEn=DateTime.UtcNow });
        repo.GuardarPregunta(new PreguntaExamen { Id="p1", ExamenId="e1", Orden=1, Tipo=TipoPregunta.McUna, Enunciado="¿?", Puntos=1, DatosJson="{}" });
        repo.GuardarRespuesta(new RespuestaUsuario { Id="r1", ExamenId="e1", PreguntaId="p1", Correcta=true, PuntosObtenidos=1 });

        var vm = new ResultadoExamenVm(repo, null!, null!);
        vm.Cargar("e1", An());

        Assert.Contains("7", vm.NotaLegible);
        Assert.True(vm.Aprobado);
        Assert.Single(vm.Detalle);
        Assert.True(vm.Detalle[0].EsCorrecta);
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test tests/Resumenes.Ui.Tests -c Debug --filter ResultadoExamenVmTests`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `ResultadoExamenVm`**

Crear `src/Resumenes.Ui/ViewModels/ResultadoExamenVm.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;
using Resumenes.Ui.Servicios;
using Resumenes.Ui.Vistas;

namespace Resumenes.Ui.ViewModels;

public partial class ResultadoExamenVm : VistaModeloBase
{
    private readonly IRepositorioExamenes _repo;
    private readonly IServicioExamenes _svc;
    private readonly ServicioNavegacion _nav;
    private Analisis? _an;
    private Examen? _examen;

    [ObservableProperty] private string _titulo = "";
    [ObservableProperty] private string _notaLegible = "";
    [ObservableProperty] private string _porcentajeLegible = "";
    [ObservableProperty] private bool _aprobado;
    [ObservableProperty] private string _feedbackGeneral = "";
    public ObservableCollection<ItemResultadoVm> Detalle { get; } = new();

    public ResultadoExamenVm(IRepositorioExamenes repo, IServicioExamenes svc, ServicioNavegacion nav)
    { _repo = repo; _svc = svc; _nav = nav; }

    public void Cargar(string examenId, Analisis an)
    {
        _an = an;
        _examen = _repo.ObtenerExamen(examenId);
        Detalle.Clear();
        if (_examen is null) return;
        Titulo = _examen.Titulo;
        NotaLegible = _examen.Nota is null ? "—" : $"Nota: {_examen.Nota:0.#}";
        PorcentajeLegible = _examen.Porcentaje is null ? "" : $"{_examen.Porcentaje:0}% de acierto";
        Aprobado = _examen.Aprobado == true;
        FeedbackGeneral = _examen.FeedbackGeneral ?? "";

        var respuestas = _repo.ListarRespuestas(examenId).ToDictionary(r => r.PreguntaId);
        foreach (var p in _repo.ListarPreguntas(examenId))
        {
            respuestas.TryGetValue(p.Id, out var r);
            Detalle.Add(new ItemResultadoVm(p, r));
        }
    }

    [RelayCommand]
    private async Task Reintentar()
    {
        if (_examen is null || _an is null) return;
        try
        {
            var cfg = System.Text.Json.JsonSerializer.Deserialize<ConfigExamen>(_examen.ConfigJson);
            if (cfg is null) return;
            var nuevo = await _svc.CrearAsync(_an.Id, _examen.Titulo, cfg, System.Threading.CancellationToken.None);
            _nav.Navegar<VistaRendirExamen>(new ParametroRendir(nuevo.Id, _an));
        }
        catch { /* si falla, permanecer en el resultado */ }
    }
}

public sealed class ItemResultadoVm
{
    public ItemResultadoVm(PreguntaExamen p, RespuestaUsuario? r)
    {
        Enunciado = p.Enunciado;
        Puntos = p.Puntos;
        PuntosObtenidos = r?.PuntosObtenidos ?? 0;
        EsCorrecta = r?.Correcta == true;
        Feedback = r?.FeedbackIa ?? "";
        Ambigua = r?.Ambigua == true;
    }
    public string Enunciado { get; }
    public double Puntos { get; }
    public double PuntosObtenidos { get; }
    public bool EsCorrecta { get; }
    public string Feedback { get; }
    public bool Ambigua { get; }
    public string PuntajeLegible => $"{PuntosObtenidos:0.#}/{Puntos:0.#}";
}
```

- [ ] **Step 4: Reemplazar el stub `VistaResultadoExamen`**

Reemplazar `VistaResultadoExamen.xaml` por una `Page` con: encabezado grande con `NotaLegible` + `PorcentajeLegible` + chip Aprobado/Desaprobado (color por `Aprobado`), `FeedbackGeneral`, un `ItemsControl` de `Detalle` (cada ítem: enunciado, `PuntajeLegible`, ícono correcto/incorrecto por `EsCorrecta`, `Feedback` y marca de ambigua), y un botón "Reintentar" (`ReintentarCommand`). `.xaml.cs` con patrón Loaded consumiendo `ParametroResultadoExamen`.

- [ ] **Step 5: Registrar en DI**

En `App.xaml.cs`:
```csharp
        sc.AddTransient<ResultadoExamenVm>(sp => new ResultadoExamenVm(
            sp.GetRequiredService<Resumenes.Core.Interfaces.IRepositorioExamenes>(),
            sp.GetRequiredService<Resumenes.Core.Interfaces.IServicioExamenes>(),
            sp.GetRequiredService<ServicioNavegacion>()));
        // VistaResultadoExamen ya registrada (stub) en Task 1; confirmar sc.AddTransient<VistaResultadoExamen>();
```

- [ ] **Step 6: Correr tests, suite y build**

Run: `dotnet test tests/Resumenes.Ui.Tests -c Debug --filter ResultadoExamenVmTests`  → PASS.
Run: `dotnet test -c Debug`  → verde.
Run: `dotnet build Resumenes.sln -c Debug`  → BUILD succeeded.

- [ ] **Step 7: Verificación manual (el usuario prueba la UI)**

Cerrar `Resumenes.Ui`. Ejecutar la app con un análisis ya completado:
1. Ver resultados del análisis → "Simular examen" → historial (vacío) → "Nuevo examen".
2. Configurar (p. ej. 3 MC-una, 1 desarrollo, 10 puntos, 5 min) → "Crear examen" → rendición.
3. Responder, navegar entre preguntas, marcar para revisar; dejar correr el timer o "Entregar".
4. Ver el resultado: nota, %, aprobado, feedback por pregunta; "Reintentar" genera otro.
5. Volver a "Exámenes": el examen aparece en el historial con su nota.

- [ ] **Step 8: Commit**

```bash
git add src/Resumenes.Ui/ViewModels/ResultadoExamenVm.cs \
        src/Resumenes.Ui/Vistas/VistaResultadoExamen.xaml src/Resumenes.Ui/Vistas/VistaResultadoExamen.xaml.cs \
        src/Resumenes.Ui/App.xaml.cs tests/Resumenes.Ui.Tests/ResultadoExamenVmTests.cs
git commit -m "feat(examenes-ui): pantalla de resultado con feedback y reintentar"
```

---

## Self-Review (cobertura, Fase 5b)

- **Historial por análisis + entrada desde Resultados:** Task 1. ✅
- **Asistente de creación (tipos/cantidades/dificultad/puntos/tiempo/fuente):** Task 2. ✅
- **Rendición interactiva por los 7 tipos + timer + autoguardado + navegación + marcar revisar:** Task 3. ✅
- **Contrato `RespuestaJson` correcto por tipo:** Task 3 (`PreguntaRendirVm.ConstruirRespuestaJson`). ✅
- **Resultado: nota/%/aprobado + desglose con feedback + reintentar:** Task 4. ✅
- **Reanudar (respuestas con id determinista, upsert):** Task 3 (autoguardado) + el motor (estado EnCurso). ✅

## Notas de diseño / backlog

- **Selección de temas en el asistente** omitida en el MVP (usa todos); agregarla requiere listar temas con nombre legible (mejora).
- **Reanudar carga de respuestas previas:** `RendirExamenVm.Cargar` puebla las preguntas pero no rehidrata las respuestas ya guardadas en los controles; para una reanudación visual completa habría que leer `ListarRespuestas` y volcar los valores en cada `PreguntaRendirVm`. Queda como mejora (el autoguardado ya persiste; al entregar se corrige todo).
- **Chunking de generación, prompts editables del examen:** backlog heredado de 5a.
- Las vistas XAML se validan manualmente (sin test de UI), según decisión del proyecto.
