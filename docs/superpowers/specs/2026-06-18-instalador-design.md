# Instalador — Diseño

**Fecha:** 2026-06-18
**Sub-proyecto:** Instalador / bootstrapper de Resúmenes de Estudio (3er sub-proyecto, tras Backbone y UI).

## Objetivo

Un **instalador liviano** (pocos MB) que deje la app .NET funcionando y que **descargue todas las dependencias pesadas** en el primer arranque, **con indicación de progreso**. El total a descargar es inevitablemente grande (~1,5–2,5 GB: LibreOffice + entorno Python); lo que se minimiza es el **instalador**, no la descarga.

## Restricciones (del usuario)

1. Instalador lo más liviano posible (pocos MB).
2. Descarga **todas** las dependencias externas.
3. Muestra **progreso** de la descarga.
4. Debe quedar **todo listo para construir y publicar** el instalador (script Inno, armado de bundles, manifest, docs de hosting).

## Arquitectura (3 piezas)

### 1. Instalador Inno Setup (~5–10 MB)
- **Instalación por-usuario** (`PrivilegesRequired=lowest`): instala en `%LOCALAPPDATA%\Programs\ResumenesApp` (sin UAC).
- Incluye **solo lo chico**: la app .NET (framework-dependent), `runtime/scripts/*.py`, `runtime/fonts/*` (DejaVu, 2,7 MB), `config/settings.json`, `schema.sql` (embebido en la DLL ya).
- **.NET 9 Desktop Runtime:** detecta si está (`dotnet --list-runtimes` busca `Microsoft.WindowsDesktop.App 9.`); si falta, lo baja con la `DownloadPage` de Inno 6.1+ (con progreso) y lo instala silencioso (`/install /quiet /norestore`). Es lo único que puede requerir admin una vez.
- Crea acceso directo en Menú Inicio y opción de escritorio.
- **No** baja LibreOffice/Python/modelos: eso lo hace la app.

### 2. Descargador in-app + Onboarding
- Al primer arranque, `OnboardingVm.Verificar()` (ya existe) detecta qué falta. Se habilita **"Descargar dependencias"** (hoy es placeholder).
- Un servicio `IDescargadorDependencias` lee el **manifest.json**, baja cada bundle con **progreso (por bundle + total)**, **reanudación (HTTP Range)** y **verificación SHA-256**, y lo **descomprime** al destino por-usuario.
- Reutiliza la UI de progreso ya pulida (barra + estados + LoaderPensante).
- Al terminar, re-corre `Verificar()`; si todo OK, habilita "Ir a Inicio".

### 3. Bundles pre-armados (los produce y hospeda el usuario)
- `python-env.zip` → `…/runtime/python` (CPython portable + PyMuPDF + PaddleOCR + PaddlePaddle + fpdf2).
- `libreoffice.zip` (~350 MB) → `…/runtime/libreoffice`.
- `paddle-models.zip` → modelos PP-OCRv6 (det/rec/orientation).
- Fuentes y scripts NO son bundle: viajan en el instalador.

## Ubicación de archivos (decisión clave)

| Qué | Dónde | Por qué |
|-----|-------|---------|
| App .NET, scripts, fuentes, settings | `%LOCALAPPDATA%\Programs\ResumenesApp` | Instalación por-usuario, sin admin |
| Runtime descargado (python, libreoffice, modelos) | `%LOCALAPPDATA%\ResumenesApp\runtime\{python,libreoffice,modelos}` | **Escribible** por la app sin admin; se baja ahí; desinstala limpio |
| Workspace (análisis, PDFs, sqlite) | `%LOCALAPPDATA%\ResumenesApp\workspace` | Program Files no es escribible; ya se resuelve a ruta absoluta |
| API key (DPAPI) | `%LOCALAPPDATA%\ResumenesApp\config\deepseek.key` | Ya existente |

`settings.json` (lo deja el instalador) usa **rutas absolutas** a `%LOCALAPPDATA%\ResumenesApp\runtime\...` para `PythonExe`, `LibreOfficeDir`, `ModelosPaddle`, y a la carpeta de instalación para `ScriptsDir`/`FontsDir`. Esto evita el problema de escritura en Program Files y desacopla la ubicación de instalación de la de descarga.

## Manifest (host-agnóstico)

La app lee `ManifestUrl` (nuevo campo en `settings.json`) y baja desde donde sea (GitHub Releases recomendado; Google Drive posible con link directo `drive.usercontent.google.com/download?id=...&confirm=t`, con la salvedad del interstitial >100 MB y la cuota; servidor propio). Cambiar de host = editar el manifest, sin tocar código.

```json
{
  "version": "1.0.0",
  "bundles": [
    { "id": "python",      "url": "https://.../python-env.zip",   "sha256": "<hex>", "bytes": 734003200, "destino": "runtime/python",      "tipo": "zip" },
    { "id": "libreoffice", "url": "https://.../libreoffice.zip",  "sha256": "<hex>", "bytes": 367001600, "destino": "runtime/libreoffice", "tipo": "zip" },
    { "id": "modelos",     "url": "https://.../paddle-models.zip","sha256": "<hex>", "bytes": 83886080,  "destino": "runtime/modelos",     "tipo": "zip" }
  ]
}
```

- `destino` es relativo a la raíz de runtime por-usuario (`%LOCALAPPDATA%\ResumenesApp`).
- El manifest puede embeber una `ManifestUrl` por defecto en `settings.json`, editable.

## Componentes (interfaces)

### Core: `IDescargadorDependencias`
```csharp
public record EstadoDescarga(
    string BundleId, FaseDescarga Fase, long BytesActual, long BytesTotal,
    int BundleIndice, int BundleTotal, string Detalle);

public enum FaseDescarga { LeyendoManifest, Descargando, Verificando, Descomprimiendo, Completado, Error }

public interface IDescargadorDependencias
{
    // Baja+verifica+descomprime los bundles faltantes o inválidos del manifest.
    Task<ResultadoDescarga> DescargarFaltantesAsync(
        IProgress<EstadoDescarga> progreso, CancellationToken ct);
}

public record ResultadoDescarga(int Ok, int Salteados, int Errores, IReadOnlyList<string> Fallos);
```

### Infrastructure: `DescargadorDependencias`
- Lee `ManifestUrl` con `HttpClient`.
- Por bundle: si `destino` ya existe y un marcador `.ok` (con el SHA-256) coincide → **saltea**. Si no:
  1. **Descarga** a `bundle.zip.part` con `HttpClient` + **Range** (reanuda si hay `.part`), reportando bytes.
  2. **Verifica** SHA-256 en streaming. Si no coincide → error (reintentable), borra `.part`.
  3. **Descomprime** (`ZipFile.ExtractToDirectory`) al `destino` (limpiando el destino previo), reportando progreso por entrada.
  4. Escribe un marcador `destino/.bundle-ok` con el SHA-256 (idempotencia).
- Reintentos con backoff ante cortes de red; cancelable.

### UI: `OnboardingVm` (extender)
- Inyectar `IDescargadorDependencias`.
- `DescargarDependenciasCommand` (async): corre `DescargarFaltantesAsync` con `IProgress<EstadoDescarga>` que actualiza nuevas propiedades observables (`Descargando`, `TextoProgreso`, `FraccionBundle`, `FraccionGlobal`) ligadas en `VistaOnboarding.xaml` a una barra + texto. Al terminar, `Verificar()` y mensaje de éxito/errores.
- Botón con estados: "Descargar dependencias" → "Descargando… (LibreOffice 120/350 MB)" → "Reintentar" si falla.

## Inno Setup (`installer/Resumenes.iss`)

- `[Setup]`: `PrivilegesRequired=lowest`, `DefaultDirName={localappdata}\Programs\ResumenesApp`, `OutputBaseFilename=ResumenesSetup`, compresión `lzma2/max`, ícono, versión.
- `[Files]`: payload publicado de la app (framework-dependent, `dotnet publish -c Release`), `runtime\scripts\*`, `runtime\fonts\*`, `config\settings.json`.
- `[Icons]`: acceso directo Menú Inicio + escritorio (opcional).
- `[Code]` (Pascal): función `NetRuntimePresente()` (corre `dotnet --list-runtimes`, parsea `Microsoft.WindowsDesktop.App 9.`); si falta, `DownloadPage` baja `windowsdesktop-runtime-9.0-win-x64.exe` y lo instala `/install /quiet /norestore`.
- `[Run]`: opción "ejecutar la app al finalizar".

## Armado de bundles (`installer/build-bundles.ps1`) — entregable para el usuario

Script PowerShell documentado que el usuario corre **una vez por versión**:
1. **python-env.zip:** baja un CPython portable (python-build-standalone 3.12 win-x64), `pip install pymupdf paddleocr paddlepaddle fpdf2` dentro de él, zipea → relocatable.
2. **libreoffice.zip:** zipea `runtime\libreoffice` actual (ya funcional).
3. **paddle-models.zip:** zipea los modelos PP-OCRv6 (de `~/.paddlex/official_models` o el dir que use PaddleOCR — a confirmar en implementación; los scripts deben quedar apuntando a `runtime/modelos`).
4. Calcula **SHA-256** y tamaño de cada zip, genera/actualiza `manifest.json`.
5. Imprime instrucciones de subida (GitHub Releases / Drive / servidor) y la `ManifestUrl` a poner en `settings.json`.

> Nota de implementación a confirmar: hoy `worker_ocr.py` ignora el argumento `<modelos_dir>` y deja que PaddleOCR baje los modelos a `~/.paddlex`. Para que los modelos vengan en el bundle bajo `runtime/modelos`, hay que (a) extraer el bundle a la cache que PaddleOCR lee, o (b) ajustar `worker_ocr.py` para pasar los dirs de modelos explícitos. Se decide al implementar; preferencia: opción (b) para mantener todo bajo `runtime/`.

## Manejo de errores / integridad

- SHA-256 obligatorio por bundle: nada se usa sin verificar.
- Reanudación de descargas parciales (Range); reintento con backoff; cancelación limpia.
- Bundle ya presente y válido → no se re-baja (marcador `.bundle-ok`).
- Error por bundle: mensaje claro + botón reintentar; no rompe la app (el Onboarding sigue mostrando faltantes).
- Sin conexión: mensaje claro, reintentable.

## Testing

- **Unit (`IDescargadorDependencias` con fakes):** servidor HTTP fake / archivos locales → verifica SHA-256 OK/mal, reanudación (Range), idempotencia (marcador `.ok`), descompresión al destino, manejo de error por bundle.
- **Manual:** correr el instalador en una máquina/usuario limpio, verificar tamaño del setup, .NET runtime, descarga con progreso, y que la app procese un PDF end-to-end.

## Entregables ("todo listo para el instalador")

1. `IDescargadorDependencias` + `DescargadorDependencias` (Core/Infra) con tests.
2. Onboarding cableado (descarga + progreso) en la UI.
3. Cambios de config: `ManifestUrl`, rutas por-usuario, `settings.json` de instalación.
4. `installer/Resumenes.iss` (Inno Setup) + verificación/instalación del .NET runtime.
5. `installer/build-bundles.ps1` + `installer/manifest.template.json`.
6. `installer/README.md`: cómo publicar (armar bundles, subir, completar manifest y `ManifestUrl`, compilar el `.iss`).

## Fuera de alcance

- Auto-actualización de la app (futuro).
- Firma de código del instalador (se puede agregar; se documenta el punto en el `.iss`).
- Decisión final de host (Drive vs GitHub): el diseño es host-agnóstico; se elige al publicar.
