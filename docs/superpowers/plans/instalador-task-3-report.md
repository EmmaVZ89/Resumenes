# Task 3 — DescargadorDependencias (Infra) — Reporte

**Fecha:** 2026-06-18  
**Estado:** DONE

## Qué se hizo

1. **Test creado** (`tests/Resumenes.Tests/DescargadorDependenciasTests.cs`): exactamente el código del plan, sin modificaciones. Contiene 3 tests con el `FakeHandler` HttpMessageHandler provisto.

2. **Fallo confirmado (TDD paso rojo):** `dotnet test --filter DescargadorDependenciasTests` falló con `CS0234: El tipo o el nombre del espacio de nombres 'Instalador' no existe en el espacio de nombres 'Resumenes.Infrastructure'` — esperado por ausencia de la clase.

3. **Carpeta creada:** `src/Resumenes.Infrastructure/Instalador/` (nueva).

4. **Implementación creada** (`src/Resumenes.Infrastructure/Instalador/DescargadorDependencias.cs`): exactamente el código del plan, sin modificaciones. Incluye los records internos `ManifestDescarga` y `BundleDescarga`.

5. **Tests en verde (TDD paso verde):** los 3 tests pasaron.

## Salida exacta de `dotnet test ... --filter DescargadorDependenciasTests`

```
  Resumenes.Core -> ...\Resumenes.Core.dll
  Resumenes.Infrastructure -> ...\Resumenes.Infrastructure.dll
  Resumenes.Tests -> ...\Resumenes.Tests.dll
Serie de pruebas para ...\Resumenes.Tests.dll (.NETCoreApp,Version=v9.0)
1 archivos de prueba en total coincidieron con el patrón especificado.

Correctas! - Con error:     0, Superado:     3, Omitido:     0, Total:     3, Duración: 92 ms - Resumenes.Tests.dll (net9.0)
```

## Auto-review

- **SHA-256 ok:** cubierto por `Descarga_verifica_y_descomprime_un_bundle` (SHA correcto → r.Ok=1).
- **SHA-256 inválido:** cubierto por `Sha_invalido_reporta_error_y_no_descomprime` (SHA "00DEADBEEF" → r.Errores=1, directorio no creado).
- **Idempotencia por marcador `.bundle-ok`:** cubierto por `Saltea_si_ya_esta_instalado_con_sha_valido` (segunda llamada → r.Salteados=1, VecesZip=1).
- **Descompresión:** verificada en el primer test (`hola.txt` con contenido "mundo" extraído correctamente, `.bundle-ok` presente).
- **Nada de más agregado:** el código es literal del plan.
- **Interfaz:** `DescargadorDependencias : IDescargadorDependencias` implementa exactamente `Task<ResultadoDescarga> DescargarFaltantesAsync(IProgress<EstadoDescarga>?, CancellationToken)`.

## Inquietudes

Ninguna. El código del plan compiló sin ningún ajuste. Los warnings de build son preexistentes al proyecto (DPAPI CA1416 en `DpapiAlmacenSecretos.cs`; CS8604 en `ServicioAnalisis.cs`) — no introducidos por esta tarea.

## Ledger

Task 3 marcada como ✅ en `docs/superpowers/plans/progress-instalador.md`.

---

## Correcciones post-review (2026-06-18)

### Fix 1 — Guard de reanudación endurecido

**Archivo:** `src/Resumenes.Infrastructure/Instalador/DescargadorDependencias.cs`

Reemplazado el bloque inicial de `DescargarConReanudacionAsync`. La lógica anterior borraba el `.part` solo si `yaTengo > b.Bytes`, pero dejaba pasar el caso `b.Bytes == 0` (tamaño desconocido) con `yaTengo > 0`, lo que podía enviar un header `Range` incorrecto. El nuevo bloque evalúa `puedeReanudar = b.Bytes > 0 && yaTengo > 0 && yaTengo < b.Bytes`; cualquier condición fuera de eso borra el `.part` y reinicia desde cero.

### Fix 2 — Test de reanudación real con 206

**Archivo:** `tests/Resumenes.Tests/DescargadorDependenciasTests.cs`

(a) `Manifest` ahora acepta `long bytes = 0` como segundo parámetro opcional; los tres tests existentes siguen compilando sin cambios.

(b) Nuevo test `Reanuda_descarga_parcial_con_Range_206`: pre-crea un `.part` con los primeros 10 bytes del zip, invoca `DescargarFaltantesAsync`, y verifica que el bundle se complete correctamente (r.Ok=1, contenido extraído).

### Salida exacta de `dotnet test --filter DescargadorDependenciasTests --nologo`

```
Correctas! - Con error:     0, Superado:     4, Omitido:     0, Total:     4, Duración: 61 ms - Resumenes.Tests.dll (net9.0)
```

### Inquietudes

Ninguna. Los warnings de build son preexistentes (CA1416 en `DpapiAlmacenSecretos.cs`, CS8604 en `ServicioAnalisis.cs`), no introducidos por estos fixes.
