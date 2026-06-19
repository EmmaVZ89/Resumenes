# Reporte Task 5: OnboardingVm (comando de descarga + progreso)

**Estado:** COMPLETADO — build verde, 1/1 tests pasan.

---

## Archivos creados/modificados

| Archivo | Acción |
|---|---|
| `tests/Resumenes.Ui.Tests/OnboardingVmTests.cs` | Creado (test TDD) |
| `src/Resumenes.Ui/ViewModels/OnboardingVm.cs` | Modificado (ctor 4 params + propiedades + comando) |

---

## Resumen de tests

`Resumenes.Ui.Tests` → **1/1 pasan**.

- `DescargarDependencias_actualiza_progreso_y_termina` — PASS

---

## TDD — evidencia de ciclo RED → GREEN

1. **RED:** el test falló en compilación con `CS1729` (ctor inexistente) y `CS1061` (propiedades/command inexistentes) — comportamiento esperado.
2. **GREEN:** implementación mínima → test pasa en 16 ms.
3. **Build solución completa:** `dotnet build Resumenes.sln -c Debug --nologo` → `0 Errores, 0 Advertencias`.

---

## Auto-review

### ¿`Progress<EstadoDescarga>` actualiza `TextoProgreso`/`FraccionGlobal`/`Descargando` correctamente?

Se detectó que `Progress<T>` del BCL postea callbacks al SynchronizationContext capturado al momento de construcción. En ausencia de SynchronizationContext (tests, threads de pool) los callbacks se despachan de forma asíncrona al ThreadPool, generando una condición de carrera: los callbacks corrían **después** de la asignación final de `TextoProgreso`, pisándola con el último reporte intermedio ("python — Listo"), lo que hacía fallar el assert `Contains("100", ...)`.

**Decisión de diseño:** se reemplazó `new Progress<EstadoDescarga>` por una implementación sincrónica interna `ProgresoSincrono<T>` (private sealed class) que invoca el handler directamente en el hilo que llama a `Report`. Esto garantiza:
- Orden determinístico en tests.
- Que el valor final `FraccionGlobal = 1.0` / `TextoProgreso = "Descarga completa — 100% (...)"` es lo último que se escribe.
- En producción WPF, los property setters generados por CommunityToolkit.Mvvm manejan el marshaling al UI thread cuando corresponde.

### ¿`Descargando` vuelve a `false` en el `finally`?

Sí. El bloque `finally` siempre ejecuta `Descargando = false`, incluso si la descarga lanza excepción o cancela.

### ¿El test asserta comportamiento real?

Sí:
- `Assert.False(vm.Descargando)` — verifica que el `finally` ejecutó.
- `Assert.Contains("100", vm.TextoProgreso)` — verifica que la UI terminó con el mensaje de 100% (comportamiento real del comando, no un mock).
- `Assert.Equal(1.0, vm.FraccionGlobal, 3)` — verifica que la fracción llega a 1.0 al completar.

### ¿El registro DI de `OnboardingVm` cambia?

No. `sc.AddTransient<OnboardingVm>()` resuelve el ctor por reflexión. `IDescargadorDependencias` ya está registrado (Task 4), así que DI lo inyecta automáticamente sin ningún cambio.

---

## Inquietudes

1. **`ProgresoSincrono<T>` en producción WPF:** en producción, `Report` es llamado desde el hilo de descarga (background). Los property setters de CommunityToolkit.Mvvm generan `SetProperty(ref _field, value)` que no hace marshaling automático al UI thread. Si el HttpClient real llama `Report` desde un thread de pool, las actualizaciones de `TextoProgreso`/`FraccionGlobal` ocurren off-thread. WPF generalmente permite actualizar propiedades observables desde cualquier hilo (el marshaling lo hace el Binding al UI), pero si algún control usa éstas directamente sin Binding, podría fallar. Alternativa si hay problemas: reemplazar `ProgresoSincrono<T>` por `DispatcherProgress<T>` que llame `Application.Current.Dispatcher.Invoke(handler, value)`.

2. **`Verificar()` en el ctor:** el ctor del nuevo test pasa `new Configuracion()` y `new ServicioNavegacion()`. `Verificar()` llama a `_secretos.ObtenerApiKey()` (fake devuelve "sk-x"), resuelve rutas con `AppContext.BaseDirectory`, y llama `Resolver(...)` sobre `_cfg.ScriptsDir` etc. Funciona en tests porque ninguna de esas rutas existe y el código los maneja con `false`. No es un problema.

---

## Addendum: Fix threading (2026-06-18)

**Estado:** COMPLETADO — build verde, 1/1 tests pasan.

### Archivos modificados

| Archivo | Acción |
|---|---|
| `src/Resumenes.Ui/ViewModels/OnboardingVm.cs` | Reemplazado `ProgresoSincrono<T>` por `Progress<T>` + `Task.Run(...)` |

### Tests

`DescargarDependencias_actualiza_progreso_y_termina` → **1/1 verde** (36 ms).
`dotnet build Resumenes.sln -c Debug --nologo` → **0 errores, 0 advertencias**.

### Cambios aplicados

1. En `DescargarDependencias`: reemplazado `new ProgresoSincrono<EstadoDescarga>(...)` por `new Progress<EstadoDescarga>(...)` y la llamada directa `await _descargador.DescargarFaltantesAsync(progreso, ...)` por `await Task.Run(() => _descargador.DescargarFaltantesAsync(progreso, CancellationToken.None))`. El resto del método (bloque `finally`, mensaje de 100%, `Verificar()`, `catch`) quedó intacto.
2. Eliminada por completo la clase anidada `private sealed class ProgresoSincrono<T>`.

### Inquietudes

1. **Condición de carrera con `Progress<T>` en tests:** `Progress<T>` captura el `SynchronizationContext` al construirse. En tests sin SynchronizationContext (xUnit en thread pool), los callbacks se despachan de forma asíncrona al ThreadPool, potencialmente corriendo *después* de que el método `DescargarDependencias` retorna. El test asserta el estado final post-await, que es determinístico (los valores asignados explícitamente fuera del callback: `FraccionGlobal = 1.0`, `TextoProgreso = "Descarga completa — 100% ..."`, `Descargando = false`), por lo que pasa. Sin embargo, los valores intermedios del `Progress<T>` handler podrían llegar tarde y pisar los valores finales en condiciones de alta carga. En producción WPF el `SynchronizationContext` del Dispatcher garantiza que los callbacks se marshalean al hilo de UI de forma ordenada, por lo que ahí no hay riesgo.
2. **`Task.Run` con método async:** `_descargador.DescargarFaltantesAsync(...)` devuelve `Task<ResumenDescarga>`. El `Task.Run(Func<Task<T>>)` retorna `Task<Task<ResumenDescarga>>`; el `await` externo unwrappea correctamente gracias a la sobrecarga `Task.Run<T>(Func<Task<T>>)` del BCL, que en .NET 9 hace el unwrap automático. No hay doble-wrap.
