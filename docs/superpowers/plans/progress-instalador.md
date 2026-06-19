# Progreso — Instalador (sin git)

Adaptaciones: SIN git (sin commits/worktree). Las revisiones leen los archivos creados/modificados directamente. Cierre de tarea = build + tests en verde.

| Task | Descripción | Estado |
|------|-------------|--------|
| 1 | Contratos del descargador (Core) | ✅ 2026-06-18 |
| 2 | Config (ManifestUrl + RutaRuntime) | ✅ 2026-06-18 |
| 3 | DescargadorDependencias (Infra) + tests | ✅ 2026-06-18 |
| 4 | DI + rutas por-usuario (App.xaml.cs) | ✅ 2026-06-18 (build 0 err; review por diff) |
| 5 | OnboardingVm (comando descarga + progreso) | ✅ 2026-06-18 (1/1 test; fix threading: Task.Run + Progress<T>) |
| 6 | VistaOnboarding.xaml (botón + progreso) | ✅ 2026-06-18 (build 0 err; verif. visual fina = usuario) |
| 7 | Inno Setup (.iss) + settings.instalacion.json + publish | ✅ 2026-06-18 (publish OK; compilar .iss = paso manual, falta ISCC) |
| 8 | build-bundles.ps1 + manifest.template.json | ✅ 2026-06-18 (parse OK; fix BOM: descargador tolerante 4/4 + PS sin BOM) |
| 9 | README de publicación + verificación final | ✅ 2026-06-18 (build 0 err; 35/35 tests; consistencia de rutas OK) |

## Revisión final (whole-branch, opus)

Veredicto: con reservas → fixes aplicados (build 0 err, 36/36 tests):
- C1 (Critical, pérdida de datos): el bundle de modelos borraba `~/.paddlex` global → **extracción no destructiva** (flag `limpiarDestino:false`) + test. ✅
- C2 (worker ignora dir de modelos): se mantiene **opción (a)** (modelos en `~/.paddlex` = donde apunta `ModelosPaddle`), autorizada por el spec. Documentado.
- I1 (.iss no chequeaba install de .NET): **chequeo de ResultCode + MsgBox**. ✅
- I2 (Onboarding no verificaba modelos): **requisito "Modelos de OCR" agregado**. ✅
- M1 (AppId en .iss): agregado. dedup en App.xaml.cs: hecho. ✅

## Ícono de la app (2026-06-18)

Logo solicitado por el usuario (birrete + libro abierto + estrella) integrado:
- Origen: `C:\Users\emmanuelz\Downloads\Gemini_Generated_Image_c8vte5c8vte5c8vt.png` (1408×768) → recorte cuadrado centrado 768×768.
- Forma final (elegida por el usuario): **transparente con esquinas redondeadas** (no recorte cuadrado con margen oscuro). Rounded-rect del diseño medido en (x415..998, y78..725 ≈ 583×647); recortado sobre lienzo cuadrado transparente con máscara de esquinas redondeadas (radio 118).
- `src/Resumenes.Ui/Recursos/app.ico` (multi-tamaño 256/64/48/32/16, frames PNG con alfa Bgra32). Validado: estructura ICO correcta + decodifica con WIC/WPF + transparencia OK en tablero.
- `Resumenes.Ui.csproj`: `<ApplicationIcon>` (ícono del exe → atajos/Explorador) + `<Resource Include>` (runtime).
- `MainWindow.xaml`: `Icon="/Recursos/app.ico"` (ventana + barra de tareas) + `ui:ImageIcon` en la TitleBar.
- `installer/Resumenes.iss`: `SetupIconFile=..\src\Resumenes.Ui\Recursos\app.ico` (ícono del instalador).
- Build 0 err; ícono confirmado embebido en el exe.

## Bundles generados (2026-06-19)

Generados en `dist/bundles/` (listos para subir):
- `python-env.zip` — 332 MB. CPython 3.12.13 relocatable (python-build-standalone) + PyMuPDF 1.27.2.3, paddleocr 3.7.0, paddlepaddle 3.3.1, fpdf2 2.8.7 (versiones idénticas al entorno de dev). Imports verificados.
- `libreoffice.zip` — 485 MB. `runtime/libreoffice` portable.
- `paddle-models.zip` — 141 MB. `~/.paddlex/official_models` (PP-OCRv6_medium det/rec, doc_ori, textline_ori, UVDoc).
- `manifest.json` — SHA-256 + bytes reales. **Total a descargar en onboarding: ~958 MB.**

Verificaciones:
- Rutas del manifest ↔ `settings.instalacion.json`: coinciden (python→runtime/python, libreoffice→runtime/libreoffice, modelos→%USERPROFILE%\.paddlex\official_models).
- Estructura interna de zips correcta (python.exe en raíz; program/soffice.exe; carpetas de modelos en raíz).
- **Bug detectado y corregido**: `build-bundles.ps1` no emitía `limpiarDestino` → el manifest dejaba el bundle de modelos con el default `true`, reintroduciendo el borrado de `~/.paddlex` (regresión del fix C1). Arreglado en el script + manifest regenerado (`limpiarDestino:false` solo en modelos).
- Separadores `\` en los zips (limitación de ZipFile en PS 5.1): verificado empíricamente que `ZipFile.ExtractToDirectory` en **.NET 9/Windows** (el descargador real) los trata como subcarpetas → extracción correcta. Nota dejada en el script.

## Publicación completada (2026-06-19)

- **Código en GitHub**: github.com/emmavzmymtec/Resumenes (público, `main`, commit inicial 862ee4b). `.gitignore`/`.gitattributes`/`README`/`LICENSE` (MIT) incluidos; sin material sensible ni binarios pesados.
- **Release `v1.0.0` publicado** con los 4 assets (python-env.zip, libreoffice.zip, paddle-models.zip, manifest.json). URLs verificadas: HTTP 200 + Content-Length exacto en los 3 zips.
- `manifest.json` del release con URLs reales de GitHub y `limpiarDestino` correcto (modelos=false). `config/settings.instalacion.json` → `ManifestUrl` apuntando al release.
- **Inno Setup** instalado per-user (winget, sin admin).
- **`dist/ResumenesSetup.exe` compilado: 5.93 MB** (cumple "mínimo MB"; las dependencias ~958 MB se bajan en el onboarding). Ícono correcto embebido.

Pendiente (solo prueba del usuario): ejecutar `ResumenesSetup.exe` en una máquina/usuario limpio → onboarding descarga dependencias → procesar un PDF de prueba.

## Hallazgos Minor (para la revisión final)

- Task 3: si `ZipFile.ExtractToDirectory` falla a mitad, el `.part` verificado persiste (se auto-recupera y la extracción se reintenta desde destino limpio). Aceptado como borde tolerable (el `.part` válido incluso ayuda a reanudar).
- Task 3: los 3 tests originales usan `bytes:0` en el manifest, así que no ejercitan el cálculo de porcentaje de progreso (el del progreso se cubre en Task 5). El test de reanudación nuevo sí usa `bytes` real.
- Task 4: `App.xaml.cs` calcula `%LOCALAPPDATA%\ResumenesApp` dos veces (`raizDatos` para RutaRuntime y `appDataDir` para secretos). Duplicación trivial; se podría unificar en la revisión final.
- Task 3/8: el `TrimStart` del descargador usa un carácter BOM literal (U+FEFF, confirmado por codepoint) en vez del escape `'﻿'`. Funcionalmente correcto; cambiar al escape mejoraría la legibilidad.
