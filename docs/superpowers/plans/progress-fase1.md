# Progreso de ejecución — Fase 1 (sin git)

Toolchain (2026-06-17): .NET 9.0.200 OK · Python 3.12 + PyMuPDF OK · fpdf2/PaddlePaddle/PaddleOCR instalándose · DejaVu pendiente.
Adaptaciones: **net9.0** (no net10.0) · **sin git** (no commits/worktree/.gitignore) · progreso en este archivo + lista de tareas.

| Task | Descripción | Estado |
|------|-------------|--------|
| 0 | Bootstrap solución (4 proyectos, paquetes) | ✅ completa (build OK, 0 errores) |
| 1 | Modelos (enums + entidades) | ✅ |
| 2 | Interfaces (puertos) | ✅ |
| 3 | Hashing (TDD) | ✅ 2/2 |
| 4 | EscrituraAtomica (TDD) | ✅ 2/2 |
| 5 | PipelineOrquestador (TDD) | ✅ 5/5 |
| 6 | SqliteRepositorioEstado + schema (TDD) | ✅ 2/2 (fix: FK ON, tipo CHECK PascalCase) |
| 7 | DeepseekClienteIA (TDD) | ✅ 2/2 |
| 8 | Adaptadores OCR + ProtocoloOcr (TDD) | ✅ 3/3 (worker adaptado a PaddleOCR 3.7) |
| 9 | PythonGeneradorPdf + DpapiAlmacenSecretos | ✅ (PDF smoke OK; RESUMENES_FONTS) |
| 10 | Cli composition root | ✅ (build OK, Polly v8) |
| 11 | Verificación end-to-end (deps + API key) | ✅ COMPLETA — `analisisfinal1.pdf` generado, 0 error, tildes/ñ correctas; idempotencia validada (re-run = salteados) |

REC A aplicada y verificada (2026-06-17, run limpio 6 ok / 0 error):
- Modelo Deepseek -> `deepseek-v4-flash` (verificado, funciona) en https://api.deepseek.com.
- Carpeta dinámica: se quitó el literal `id_AnalisisFinal`; los resultados van bajo `analisis/<analisis_id>/` con `<analisis_id>` legible = `{nombre}-{8hex}` (p. ej. prueba-28218768). Subcarpetas: 00_fuentes/consolidado/resumen/final.
- Workspace movido DENTRO del proyecto: `D:\...\Resumenes\workspace\`. La API key se desacopló a %LOCALAPPDATA%\ResumenesApp\config\deepseek.key (secreto, fuera del árbol del proyecto).
- #2 robustez orquestador: el GuardarUnidad de EnProceso está dentro del try (un fallo de persistencia marca Error sin crashear); el catch guarda el error de forma protegida.
- Bonus: Analisis.estado se cierra en Completado/ConErrores al finalizar el run.
NOTA doc: especificacionesV2.txt aún menciona "deepseek-chat" e "id_AnalisisFinal" (sincronizar si se quiere; el código es la fuente operativa).

== FASE 2 COMPLETA Y VERIFICADA (2026-06-17) ==
Lote multi-archivo, decisión = 1 PDF por archivo (la consolidación por temas con IA queda para Fase 4).
Cambios: ConstructorPipeline ahora es por-archivo (temaId = t-<archivo_id>, orden por índice, PDF nombrado por el archivo); Program.cs hace loop sobre todos los .pdf de la carpeta con TOLERANCIA por-archivo (un archivo que falla no tumba el lote), pre-vuelo (API key + scripts + fuentes presentes), fingerprint de carpeta sobre todos los archivos, reporte de lote "N ok / M error", Analisis.estado Completado/ConErrores.
Run real verificado: carpeta con 3 PDFs (2 válidos + roto.pdf basura) -> "2 ok / 1 error" (EXIT=2), roto.pdf reportado sin crashear, 2 PDFs distintos generados (comercio2.pdf, con_tildes.pdf) con contenido correcto y tildes/ñ. 16/16 tests .NET siguen verdes.
== FASE 3 COMPLETA Y VERIFICADA (2026-06-17) ==
Decisión Office: render→OCR con LibreOffice (el usuario priorizó NO perder info que vive en imágenes embebidas; texto-nativo-solo se descartó porque obvia las imágenes).
LibreOffice 26.2.4 PORTABLE dentro del proyecto: admin-extract del MSI a runtime/libreoffice/ (soffice.exe en runtime/libreoffice/program/soffice.exe), SIN instalación a nivel sistema. ~1 GB en el proyecto.
Código: IConversorOffice + LibreOfficeConversor (soffice --headless --convert-to pdf, perfil aislado); ConstructorPipeline ramifica Captura/OcrBruto por tipo (PDF directo / Office→LibreOffice→rasterizar / TXT directo sin OCR); Chunking helper + chunking en LimpiezaIA y ResumenFinal (umbral cfg.MaxCharsIA=16000); Program glob multi-extensión + TipoDe() + pre-vuelo de LibreOffice + RUTA ABSOLUTA al pipeline (soffice no resuelve rutas relativas). 16/16 tests.
Run verificado: carpeta con cadena.pptx + logistica.docx -> 2 ok / 0 error, 2 PDFs (cadena.pdf, logistica.pdf), y CLAVE: el texto que estaba SOLO en una imagen embebida (Shanghai/47M TEU/FOB) apareció en el resumen -> confirmado que render→OCR captura info de imágenes. Fix de esta fase: ruta relativa->absoluta (soffice fallaba "source file could not be loaded").
Nota: python-docx/python-pptx instalados solo para crear fixtures (no son del pipeline). OCR sobre texto chico de imagen sale algo ruidoso (inherente a OCR; mejorable con más DPI).
== FASE 4 COMPLETA Y VERIFICADA (2026-06-17) ==
Consolidación por temas cross-archivo. Decisiones: temas.json editable por re-corrida (si existe, se respeta; idempotencia regenera solo lo de abajo); SOLO PDF por tema (el por-archivo llega hasta texto limpio). SIN cambio de esquema (se reúsan las etapas: Captura/OcrBruto/LimpiezaIA por-archivo; ConsolidacionTemas/ResumenFinal/GeneracionPDF por-tema).
Arquitectura en 3 fases (Program.cs): (1) por archivo hasta limpio (tolerancia); (2) DetectorTemas.cs: IA sobre los limpios -> temas.json {temas:[{id,nombre,orden,archivos[]}]} + Tema/TemaArchivo en SQLite (si temas.json existe, se carga y respeta); (3) por tema: ConsolidacionTemas (junta los limpios asignados) -> ResumenFinal (IA estructurado, enfocado al tema) -> PDF por tema. Arg opcional --temas "t1,t2" como pista. ConstructorPipeline partido en PasosPorArchivo()/PasosPorTema(). 16/16 tests, build OK.
Run verificado: 3 TXT (geo1,geo2,puertos1) -> 2 temas detectados correctamente (Geografía={geo1,geo2}, Puertos={puertos1}), 2 PDFs por tema; el PDF de Geografía fusionó ambos archivos. EXIT=0.

ESTADO GLOBAL: el flujo funcional completo de la V1 está operativo (carpeta con PDF/DOCX/PPTX/TXT -> por archivo -> agrupar por temas -> PDF de estudio por tema), salvo la UI.
Pendiente: UI WPF (wizard/historial/progreso/abrir PDF/editar temas). Empaquetado: instalador (Inno Setup/WiX) bundleando .NET+Python+PaddleOCR+LibreOffice+modelos+fuentes. Pulido: sincronizar especificacionesV2.txt (dice deepseek-chat/id_AnalisisFinal); tests unitarios a los fixes de integración (stderr-drain, Resolver, BOM, ruta absoluta, Tema-upfront, chunking, detección de temas).

== FASE 1 COMPLETA Y VERIFICADA END-TO-END (2026-06-17) ==
Run real: PDF fixture → rasterizar (PyMuPDF) → OCR (PaddleOCR 3.7) → limpieza (Deepseek) → consolidación → resumen estructurado (Deepseek) → PDF (fpdf2). Salida correcta con ortografía restaurada por la IA. 16/16 tests .NET.

Bugs reales encontrados y corregidos durante la ejecución (que el plan/tests no detectaron):
1. FK desactivada por el subagente para pasar un test → restaurada (PRAGMA foreign_keys=ON).
2. schema `CHECK(tipo)` en minúsculas vs enum PascalCase → GuardarArchivo fallaba siempre. Unificado a PascalCase.
3. Resolver de rutas relativo solo al .exe → no encontraba runtime/scripts en dev. Fallback a cwd.
4. Deadlock: PaddleOcrServicio no drenaba stderr → worker bloqueado. Drenado en tarea paralela.
5. Cuelgue: worker usaba `for line in sys.stdin` (read-ahead buffering) → no recibía el pedido. Cambiado a readline().
6. UTF-8 BOM: C# StandardInputEncoding=Encoding.UTF8 escribía BOM → JSON inválido en el worker. Cambiado a UTF8Encoding(false) + worker descarta BOM.
7. FK ordering: orquestador persistía Unidad por-tema antes de que el paso creara el Tema. Tema creado por adelantado en ConstructorPipeline.
Entorno: .NET 9 (no 10); PaddleOCR 3.7 requiere enable_mkldnn=False + PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK=True en este Windows; DejaVu descargadas a runtime/fonts.

Follow-ups menores (NO bloquean Fase 1; para Fase 2 o pulido):
- Analisis.estado nunca pasa a Completado/ConErrores al terminar el run.
- Orquestador: el GuardarUnidad de "EnProceso" está fuera del try → un fallo de persistencia crashea en vez de marcar Error.
- Workspace en %LOCALAPPDATA% (D-A4) vs "todo dentro de D:\...\Resumenes" (revisar con el usuario).
- Los fixes nuevos (stderr drain, Resolver, Tema-upfront, BOM) están validados por el e2e pero sin tests unitarios.
- worker `<modelos_dir>` arg sin usar (PaddleOCR usa cache ~/.paddlex); bundling local de modelos = fase posterior.
