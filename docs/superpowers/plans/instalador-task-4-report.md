# Task 4: DI + rutas por-usuario — Reporte

## Estado
COMPLETADO

## Archivos modificados
- `src/Resumenes.Ui/App.xaml.cs`

## Build
`Compilación correcta. 0 Errores. 2 Advertencias (preexistentes, CS0618 en MainWindow.xaml.cs, no relacionadas con Task 4).`

## Cambios aplicados

### 1. Cálculo de `RutaRuntime` + expansión de env-vars (antes de `sc.AddSingleton(cfg)`)
Insertado inmediatamente después de la carga de `cfg` y antes de registrar los adaptadores:
- Si `cfg.RutaRuntime` está vacío, se calcula como `%LOCALAPPDATA%\ResumenesApp\runtime`.
- Se aplica `Environment.ExpandEnvironmentVariables` a `RutaRuntime`, `PythonExe`, `LibreOfficeDir`, `ModelosPaddle`, `ScriptsDir`, `FontsDir` y `ManifestUrl`.
- El bloque queda ANTES de `sc.AddSingleton(cfg)` y por tanto ANTES de que los adaptadores usen esas rutas.

### 2. Registro de `IDescargadorDependencias` en DI (junto a servicios de UI)
Agregado debajo de `sc.AddSingleton<Wpf.Ui.IContentDialogService, ...>()`:
```csharp
sc.AddSingleton<Resumenes.Core.Interfaces.IDescargadorDependencias>(_ =>
    new Resumenes.Infrastructure.Instalador.DescargadorDependencias(
        new HttpClient { Timeout = TimeSpan.FromMinutes(60) }, cfg.ManifestUrl, cfg.RutaRuntime));
```
Usa nombre completo calificado; no requiere using adicional.

## Auto-review
- `IDescargadorDependencias` resuelve con `HttpClient` dedicado (60 min timeout), `cfg.ManifestUrl` y `cfg.RutaRuntime`, ambos ya expandidos. OK.
- La expansión de env-vars y el cálculo de `RutaRuntime` quedan ANTES de `sc.AddSingleton(cfg)` y de todos los adaptadores. OK.
- No se duplicaron registros; `OnboardingVm` sigue registrado como `sc.AddTransient<OnboardingVm>()` (sin cambios). OK.
- El flujo `--demo` no fue tocado. OK.

## Inquietudes
Ninguna. El warning CS0618 es preexistente (SetDialogHost deprecado en WPF-UI) y no afecta esta tarea.
