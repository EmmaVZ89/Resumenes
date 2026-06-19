# Task 8 — Reporte: build-bundles.ps1 + manifest.template.json

**Estado:** COMPLETADO

## Archivos creados

- `installer/manifest.template.json`
- `installer/build-bundles.ps1`

## Resultado del parse-check

```
OK parse
```

(`[System.Management.Automation.Language.Parser]::ParseFile` — 0 errores de sintaxis.)

## Auto-review de consistencia

| Bundle   | `destino` en manifest.template.json                     | `destino` en Entry() del script                       | Coincide |
|----------|---------------------------------------------------------|-------------------------------------------------------|----------|
| python   | `"python"`                                              | `"python"`                                            | SI       |
| libreoffice | `"libreoffice"`                                      | `"libreoffice"`                                       | SI       |
| modelos  | `"%USERPROFILE%\\.paddlex\\official_models"`            | `"%USERPROFILE%\.paddlex\official_models"`            | SI       |

- SHA-256: calculado con `Get-FileHash -Algorithm SHA256` sobre el zip real (bytes reales, no estimados).
- Bytes: `(Get-Item $zip).Length` — tamaño real del zip en bytes.
- El script usa `[System.IO.Compression.ZipFile]::CreateFromDirectory` (no `Compress-Archive`), alineado con el tech stack.

## Inquietudes / notas

- Ninguna bloqueante. El script guarda con `-Encoding utf8` (UTF-8 sin BOM en PS 5.1 vía `Set-Content`). Si se necesita BOM explícito, cambiar a `-Encoding utf8BOM` (PS 7+) o usar `[System.IO.File]::WriteAllText`. Para el uso previsto (ser leído por `JsonSerializer` de .NET) no importa.
- El bundle `modelos` usa `ZipDir $modelos $zipMo` pero `$modelos` apunta a `%USERPROFILE%\.paddlex\official_models` expandido por PS. El `destino` en el manifest queda como literal `%USERPROFILE%\.paddlex\official_models` (sin expandir), que es lo correcto para que `DescargadorDependencias` lo expanda en la máquina del usuario final.

---

# Task 8 — Addendum: Robustez de encoding BOM

**Estado:** COMPLETADO  
**Fecha:** 2026-06-18

## Archivos modificados

- `src/Resumenes.Infrastructure/Instalador/DescargadorDependencias.cs`
- `installer/build-bundles.ps1`

## Cambios aplicados

**Cambio 1 — Descargador tolerante a BOM** (`DescargadorDependencias.cs`, línea 17):  
`GetStringAsync` ahora aplica `.TrimStart('﻿')` sobre el JSON descargado antes de deserializar. Defensivo y no-op cuando el manifest no tiene BOM.

**Cambio 2 — Script genera manifest sin BOM** (`build-bundles.ps1`, línea 50):  
Reemplazado `Set-Content -Encoding utf8` (que en PS 5.1 escribe UTF-8 CON BOM) por `[System.IO.File]::WriteAllText(…, New-Object System.Text.UTF8Encoding($false))` (UTF-8 sin BOM garantizado en PS 5.1 y PS 7+).

## Verificaciones

- Tests `DescargadorDependenciasTests`: **4/4 verde** (TrimStart es no-op sobre manifests sin BOM en fixtures de test).
- Parse-check `build-bundles.ps1`: **OK parse** (0 errores de sintaxis vía `[System.Management.Automation.Language.Parser]::ParseFile`).

## Inquietudes

Ninguna. Ambos cambios son aditivamente seguros: el TrimStart solo actúa si hay BOM; la escritura sin BOM es compatible con cualquier consumidor JSON estándar.
