# Reporte Task 1 y Task 2 — Instalador

Fecha: 2026-06-18

## Task 1: Contratos del descargador (Core)

### Archivo creado
`src/Resumenes.Core/Interfaces/IDescargadorDependencias.cs`

Contenido literal del plan:
- `enum FaseDescarga { LeyendoManifest, Descargando, Verificando, Descomprimiendo, Completado, Error }`
- `record EstadoDescarga(string BundleId, FaseDescarga Fase, long BytesActual, long BytesTotal, int BundleIndice, int BundleTotal, string Detalle)`
- `record ResultadoDescarga(int Ok, int Salteados, int Errores, IReadOnlyList<string> Fallos)`
- `interface IDescargadorDependencias` con método `Task<ResultadoDescarga> DescargarFaltantesAsync(IProgress<EstadoDescarga>? progreso, CancellationToken ct)`

### Resultado build Core

```
dotnet build src/Resumenes.Core/Resumenes.Core.csproj -c Debug --nologo

Compilación correcta.
    0 Advertencia(s)
    0 Errores
Tiempo transcurrido 00:00:02.59
```

---

## Task 2: Configuración (ManifestUrl + RutaRuntime)

### Archivo creado (test)
`tests/Resumenes.Tests/ConfiguracionTests.cs`

Test: `Defaults_incluyen_ManifestUrl_y_RutaRuntime`
- Verifica que `ManifestUrl` no sea nulo ni espacio en blanco
- Verifica que `RutaRuntime` no sea null (acepta string vacío)

### Fase roja (TDD)

```
dotnet test tests/Resumenes.Tests/Resumenes.Tests.csproj --filter Defaults_incluyen_ManifestUrl_y_RutaRuntime --nologo

error CS1061: "Configuracion" no contiene una definición para "ManifestUrl"
error CS1061: "Configuracion" no contiene una definición para "RutaRuntime"
```

Confirmado: falló por ausencia de propiedades.

### Archivo modificado
`src/Resumenes.Infrastructure/Aplicacion/Configuracion.cs`

Propiedades agregadas al final de la clase:
```csharp
/// <summary>URL del manifest.json con los bundles a descargar. Editable según el host.</summary>
public string ManifestUrl { get; set; } = "https://example.com/resumenes/manifest.json";
/// <summary>Raíz por-usuario donde se descomprime el runtime. Vacío = App calcula %LOCALAPPDATA%/ResumenesApp/runtime.</summary>
public string RutaRuntime { get; set; } = "";
```

### Fase verde (TDD)

```
dotnet test tests/Resumenes.Tests/Resumenes.Tests.csproj --filter Defaults_incluyen_ManifestUrl_y_RutaRuntime --nologo

Correctas! - Con error: 0, Superado: 1, Omitido: 0, Total: 1, Duración: 4 ms
```

---

## Auto-review

### ¿Faltó algo?
No. Task 1 define los 4 símbolos indicados (enum, 2 records, interfaz). Task 2 agrega las 2 propiedades exactas y el test pasa. El ledger `progress-instalador.md` quedó actualizado con ✅ en Task 1 y Task 2.

### ¿Se agregó algo de más?
No. No se implementó nada de Task 3 (DescargadorDependencias) ni Tasks posteriores.

### ¿Los tipos coinciden con lo que el plan dice que se produce?
Sí.

- Task 1 produce: `IDescargadorDependencias.DescargarFaltantesAsync(IProgress<EstadoDescarga>?, CancellationToken) : Task<ResultadoDescarga>`; `enum FaseDescarga`; `record EstadoDescarga`; `record ResultadoDescarga` — todos con los parámetros exactos del plan.
- Task 2 produce: `Configuracion.ManifestUrl` (string, default `"https://example.com/resumenes/manifest.json"`); `Configuracion.RutaRuntime` (string, default `""`) — exactamente como el plan indica.

### Warnings pre-existentes
La compilación de `Resumenes.Infrastructure` emite 5 warnings pre-existentes (CS8604 en ServicioAnalisis.cs, CA1416 x4 en DpapiAlmacenSecretos.cs). Ninguno es nuevo ni relacionado con los cambios de Task 1/2.

---

## Archivos tocados

| Archivo | Acción |
|---|---|
| `src/Resumenes.Core/Interfaces/IDescargadorDependencias.cs` | CREADO |
| `src/Resumenes.Infrastructure/Aplicacion/Configuracion.cs` | MODIFICADO (+2 propiedades) |
| `tests/Resumenes.Tests/ConfiguracionTests.cs` | CREADO |
| `docs/superpowers/plans/progress-instalador.md` | MODIFICADO (Task 1+2 → ✅) |
