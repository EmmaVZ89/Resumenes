# Reporte: Correcciones finales sub-proyecto Instalador

**Fecha:** 2026-06-18  
**Estado:** COMPLETO — todos los cambios aplicados, build limpio, tests verdes.

## Build y tests

- **Build:** `dotnet build Resumenes.sln -c Debug --nologo` → 0 errores, 7 warnings preexistentes (sin cambios respecto al baseline).
- **Tests:** `dotnet test Resumenes.sln --nologo` → **36/36 verdes** (27 en Resumenes.Tests + 9 en Resumenes.Ui.Tests).

## Archivos modificados

| Archivo | Fix |
|---|---|
| `src/Resumenes.Infrastructure/Instalador/DescargadorDependencias.cs` | C1 (campo `LimpiarDestino` en record + condicional en descompresión) |
| `installer/manifest.template.json` | C1 (`"limpiarDestino": false` en bundle `modelos`) |
| `installer/Resumenes.iss` | M1 (`AppId` al inicio de `[Setup]`) + I1 (chequeo de resultado de Exec) |
| `src/Resumenes.Ui/ViewModels/OnboardingVm.cs` | I2 (requisito "Modelos de OCR" entre LibreOffice y Carpeta de salida) |
| `src/Resumenes.Ui/App.xaml.cs` | dedup (eliminada variable duplicada `appDataDir`; se usa `raizDatos`) |
| `tests/Resumenes.Tests/DescargadorDependenciasTests.cs` | C1 (test `LimpiarDestino_false_preserva_archivos_preexistentes`) |

## Detalle por fix

- **C1:** `BundleDescarga` ahora tiene `bool LimpiarDestino = true`. El bloque de descompresión solo borra el destino si `b.LimpiarDestino`. El bundle `modelos` en el manifest template lleva `"limpiarDestino": false`. Test nuevo verifica que un archivo preexistente en el destino sobrevive la descarga.
- **BOM-escape:** El archivo ya tenía el BOM como bytes UTF-8 `\xef\xbb\xbf` (correcto). No se requirió cambio.
- **I1:** `Exec(...)` ahora dentro de `if (not ...) or (ResultCode <> 0) then MsgBox(...)`.
- **M1:** `AppId={{8F3A2C7E-5B1D-4E9A-9C2F-RES0MENES001}}` agregado como primera línea de `[Setup]`.
- **I2:** Requisito "Modelos de OCR (PaddleOCR)" agregado en `Verificar()`, usando `modelsDir` ya computado. `System.Linq` no requirió `using` adicional (ya presente implícitamente via top-level).
- **dedup:** Eliminadas las 2 líneas de `appDataDir`; `DpapiAlmacenSecretos` construido con `raizDatos` directamente.

## Inquietudes

- El campo `LimpiarDestino` se deserializa vía `JsonSerializerOptions { PropertyNameCaseInsensitive = true }`, entonces `"limpiarDestino"` (camelCase en JSON) mapea correctamente al parámetro `LimpiarDestino` del record posicional. Sin embargo, los records posicionales en C# con parámetros opcionales y `System.Text.Json` requieren que haya un constructor accesible. En .NET 9 con `PropertyNameCaseInsensitive = true`, la deserialización de records posicionales funciona por constructor, y el campo opcional con default `= true` es soportado. Verificado: el test pasa, lo que confirma que la deserialización funciona correctamente.
- El marcador `.bundle-ok` se escribe en el destino. Con `LimpiarDestino=false` y destino preexistente, si el directorio ya existe `Directory.CreateDirectory` no falla (es idempotente). Comportamiento correcto.
