# Diseño — Fase 1: Esqueleto end-to-end (backbone headless)

Fecha: 2026-06-17
Proyecto: App de Resúmenes de Material de Estudio (comercio exterior)
Spec madre: `especificacionesV2.txt` (este documento detalla solo la Fase 1)
Ubicación: todo el proyecto vive dentro de `D:\Desarrollo\Programacion\Resumenes`.

---

## 1. Objetivo y criterio de "hecho"

Construir el **esqueleto end-to-end mínimo**: un runner de **consola** (sin UI) que procese
una carpeta con **1 PDF chico** atravesando TODO el pipeline (captura → OCR → limpieza →
consolidación trivial → resumen → PDF), persistiendo estado en SQLite. El propósito es
**validar tempranamente las integraciones más riesgosas** (.NET↔Python↔PaddleOCR y .NET↔Deepseek)
antes de invertir en la UI.

**Definición de hecho (Fase 1):** se cumplen los 5 criterios de aceptación de la sección 8.

---

## 2. Decisiones de Fase 1 (cerradas en brainstorming 2026-06-17)

- **Forma:** runner de consola headless (la cáscara WPF entra en Fase 2).
- **OCR real desde Fase 1:** un PDF chico se rasteriza (PyMuPDF) y se OCR-ea (PaddleOCR vía
  worker Python). Valida el eslabón más riesgoso de entrada.
- **Deepseek real detrás de `IClienteIA`:** el run end-to-end pega a la API real (valida key
  DPAPI, contrato HTTP, reintentos); los tests automatizados usan un *fake* offline.
- **Estructura:** puertos y adaptadores liviano (enfoque B): `Core` con interfaces + runner
  fino + tests. La UI de Fase 2 referenciará `Core` sin reescritura.
- **Target framework:** .NET 10 (LTS vigente). Backbone multiplataforma salvo DPAPI (Windows),
  aislado tras interfaz.

---

## 3. Arquitectura y estructura de la solución

Solución `Resumenes.sln`, 4 proyectos:

```
Resumenes.Core            (librería; sin dependencias externas → 100% testeable)
  ├─ Modelos        Analisis, Archivo, Tema, Unidad, Etapa(enum), EstadoUnidad(enum)
  ├─ Interfaces     IRasterizador, IServicioOcr, IClienteIA, IGeneradorPdf,
  │                 IRepositorioEstado, IAlmacenSecretos, IRelojUtc
  ├─ Etapas         lógica de cada etapa (depende solo de interfaces)
  ├─ Apoyos         Hashing.Sha256, EscrituraAtomica, FormatoEstructurado (constantes)
  └─ Orquestador    PipelineOrquestador (máquina de estados + idempotencia)

Resumenes.Infrastructure  (implementaciones concretas)
  ├─ DeepseekClienteIA       : IClienteIA          (HttpClient + Polly)
  ├─ PyMuPdfRasterizador     : IRasterizador        (script Python PyMuPDF)
  ├─ PaddleOcrServicio       : IServicioOcr         (worker Python NDJSON, larga vida)
  ├─ PythonGeneradorPdf      : IGeneradorPdf        (generador_estudio_final.py)
  ├─ SqliteRepositorioEstado : IRepositorioEstado   (Microsoft.Data.Sqlite + schema.sql)
  ├─ DpapiAlmacenSecretos    : IAlmacenSecretos     (ProtectedData, CurrentUser)
  └─ ConfiguracionSerilog

Resumenes.Cli             (composition root: lee settings.json, arma DI, corre el pipeline)

Resumenes.Tests           (xUnit + fakes de cada interfaz; repo SQLite sobre archivo temporal)
```

**Flujo de dependencias:** `Cli → Infrastructure → Core`; `Tests → Core` (con fakes) +
integración selectiva contra `Infrastructure`. `Core` no conoce HttpClient, Process ni SQLite.

**Assets de runtime** (en `runtime/`, fuera de los proyectos): worker OCR (`worker_ocr.py`),
`rasterizar.py`, `generador_estudio_final.py`, `fonts/` (DejaVu) y —en producción— Python
embebido + PaddleOCR + LibreOffice. Rutas resueltas por `config/settings.json` + variables de
entorno, con default a `runtime/` relativa al ejecutable.

---

## 4. Pipeline y máquina de estados

Entrada: carpeta con 1 PDF chico. Etapas (⚙️ = simplificación de Fase 1):

- **Etapa 0 — Pre-vuelo:** validar archivo (existe, no corrupto/protegido, tipo soportado) y
  espacio en disco; validar entorno (ver §7). Crear `Analisis`, `hash_sha256` del archivo →
  `archivo_id` (16 hex), y `fingerprint` de la carpeta.
- **1. Captura** (por-archivo, sin IA): `IRasterizador` → `00_fuentes/<archivo_id>/imagenes/pagina_NNNN.jpg`, 200 DPI gris.
- **2. OcrBruto** (por-archivo, sin IA): `IServicioOcr` (worker PaddleOCR de larga vida) →
  concatena texto → `texto_bruto/bruto.txt`.
- **3. LimpiezaIA** (por-archivo, IA): `bruto.txt` → `IClienteIA` corrige OCR (no inventa,
  temp 0.1–0.3) → `texto_limpio/limpio.txt`. ⚙️ Una sola llamada (archivo chico); el método
  queda preparado para chunking en fases siguientes.
- **4. ConsolidacionTemas** ⚙️ **trivial** (1 archivo = 1 tema, **sin IA de detección**):
  crear `Tema` (orden=1, nombre derivado del archivo), copiar `limpio.txt →
  id_AnalisisFinal/consolidado/<tema_id>.txt`, persistir `TemaArchivo`, escribir `temas.json`
  autoconfirmado (headless, sin confirmación interactiva).
- **5. ResumenFinal** (por-tema, IA): `consolidado/<tema_id>.txt` → `IClienteIA` produce el
  resumen en **formato estructurado** (`#TITULO`, `@seccion`, `@texto`, `@blt`, `@ejemplo`,
  `@dato`, `@tip`) → `resumen/<tema_id>.txt`. El prompt fija el formato de marcadores.
- **6. GeneracionPDF** (por-tema): `resumen/<tema_id>.txt` → `IGeneradorPdf` invoca
  `generador_estudio_final.py` por CLI → `final/analisisfinal1.pdf`. Sin código generado por IA.

**Orquestador** — por cada unidad `(analisis, archivo|tema, etapa)`:
1. Si `Completado` y `hash_entrada` coincide → **se saltea** (idempotencia/reanudación).
2. Si no → `EnProceso` → corre la etapa vía su interfaz → escribe el artefacto **atómicamente**
   → persiste `Completado` (con `prompt_version` + `modelo_ia` en etapas con IA, y `tokens`).
3. Error: en IA, reintentos con Polly; agotados → `Error` con mensaje; **el proceso no crashea**.
4. `fijado_por_usuario = true` → nunca se reprocesa automáticamente aunque cambie el hash.

`hash_entrada` = SHA-256 de la concatenación canónica ordenada de los hashes de las entradas
de la unidad. Condición de reutilización: `hash_entrada` + `prompt_version` + `modelo_ia` iguales.

---

## 5. Contratos (interfaces en `Core`)

```
// IA
Task<RespuestaIA> IClienteIA.CompletarAsync(SolicitudIA req, CancellationToken ct);
  SolicitudIA { promptSystem, promptUser, temperatura, maxTokens, promptVersion, modelo }
  RespuestaIA { texto, finishReason, tokensPrompt, tokensCompletion, tokensTotal }

// OCR (propósito único cada una)
Task<string[]> IRasterizador.RasterizarAsync(string pdfPath, string outDir, int dpi, CancellationToken ct);
Task<string>   IServicioOcr.OcrAsync(IReadOnlyList<string> rutasImagenes, CancellationToken ct);

// PDF
Task IGeneradorPdf.GenerarAsync(string contenidoPath, string pdfPath, string titulo, string subtitulo, CancellationToken ct);

// Estado
void        IRepositorioEstado.InicializarEsquema();
Analisis?   IRepositorioEstado.ObtenerAnalisisPorFingerprint(string fingerprint);
void        IRepositorioEstado.GuardarAnalisis(Analisis a);
Unidad?     IRepositorioEstado.ObtenerUnidad(string analisisId, string? archivoId, string? temaId, Etapa etapa);
void        IRepositorioEstado.GuardarUnidad(Unidad u);
// + Archivo, Tema, TemaArchivo, Ejecucion

// Secretos / tiempo
void    IAlmacenSecretos.GuardarApiKey(string key);
string? IAlmacenSecretos.ObtenerApiKey();
DateTime IRelojUtc.Ahora();
```

**`DeepseekClienteIA`:** POST `{baseUrl}/chat/completions`, `Authorization: Bearer <key>`,
Polly (backoff + Retry-After) en 429/5xx/timeout; lee `finish_reason` (detecta `"length"`) y
`usage`. **`PaddleOcrServicio`:** worker de larga vida, protocolo NDJSON (§8 de la spec):
`{"type":"ready"}`; pedido `{"req_id","ruta_imagen"}`; respuestas `result`/`progress`/`error`
por `req_id`; .NET mata el árbol de procesos al cancelar. **`PythonGeneradorPdf`:** valida
exit 0 y que el PDF quedó en `final/`. **`SqliteRepositorioEstado`:** WAL, único escritor de la base.

---

## 6. Configuración y secretos

`config/settings.json`: `rutaWorkspace` (default `%LOCALAPPDATA%/ResumenesApp`), rutas de runtime
(`pythonExe`, `scriptsDir`, `modelosPaddle`, `libreoffice`, `fontsDir`), `dpi` (200), `modelo`
(`deepseek-chat`), `baseUrlDeepseek`. **La API key NO va en settings.json** — va cifrada con
DPAPI (`CurrentUser`) en `config/`. En desarrollo, las rutas de runtime pueden sobreescribirse
con variables de entorno (`RESUMENES_PYTHON`, `RESUMENES_FONTS`, etc.).

---

## 7. Robustez y manejo de errores

- **Pre-vuelo de entorno:** Python arranca; `fpdf2` importa; fuentes DejaVu presentes; modelos
  PaddleOCR presentes; API key + conexión válidas antes de gastar tokens. Falla → **Error de
  entorno** accionable (distinto de error de datos; no consume reintentos de IA).
- **Tolerancia por-unidad:** cada etapa en try/catch; falla → `Error` con motivo; el proceso
  termina reportando el detalle, **nunca crashea**.
- **Reintentos:** IA con Polly; OCR/PDF con timeout por unidad + reintentos acotados.
- **Reanudación:** checkpointing en SQLite; unidades `Completado` se saltean.
- **Sin huérfanos:** `Process.Kill(entireProcessTree)` al cancelar/fallar.
- **Escritura atómica** (`.tmp` + `File.Replace`) evita artefactos a medias.
- **Logging Serilog** por análisis: timeline de etapas, tokens, errores, `stderr` de Python.

---

## 8. Testing y criterios de aceptación

**Tests (xUnit + fakes):**
- Unitarios de `Core` (offline) con `FakeClienteIA`, `FakeRasterizador`, `FakeServicioOcr`,
  `FakeGeneradorPdf` y repo SQLite sobre archivo temporal:
  - Orquestador: idempotencia, reanudación, invalidación por hash, respeto de
    `fijado_por_usuario`, tolerancia a error.
  - `Hashing`, `fingerprint`, `EscrituraAtomica` (deja el destino, no deja `.tmp`).
  - Repo SQLite: corre `schema.sql`, CRUD, índice único de idempotencia.
- **Smoke test Python (pytest):** `generador_estudio_final.py` sobre `ejemplo_contenido.txt`
  produce un PDF no vacío (cubre parser + fpdf2).
- **Integración real (marcados, fuera del set por defecto):** `DeepseekClienteIA`,
  `PaddleOcrServicio`, `PythonGeneradorPdf`.

**Criterios de aceptación de Fase 1:**
1. Dado 1 PDF nativo en una carpeta → produce `final/analisisfinal1.pdf`, termina "1 ok / 0 error",
   con tildes/ñ correctas en el PDF.
2. Re-ejecutar la misma carpeta sin cambios NO reprocesa ni vuelve a gastar tokens de IA.
3. Error transitorio de Deepseek → Polly reintenta; error definitivo → unidad `Error`, sin crash.
4. Un fixture con `ñ`/tildes/`¿¡` sale correcto punta a punta (UTF-8 verificado).
5. El worker Python arranca (`ready`), procesa y cierra sin dejar procesos huérfanos.

---

## 9. Fuera de alcance de Fase 1 (fases siguientes)

UI WPF; lote de múltiples archivos; DOC/DOCX/PPT/PPTX (LibreOffice); detección de temas con IA
(consolidación real); chunking map-reduce; edición manual interactiva; acciones sobre el
resultado; historial; configuración por pantalla; instalador. (Ver roadmap de la spec madre.)

---

## 10. Prerrequisitos de entorno de desarrollo

Python con `fpdf2`, `PaddleOCR` (+ modelos `es`) y `PyMuPDF` instalados y accesibles; fuentes
DejaVu en `runtime/fonts/`; una API key de Deepseek (para el run real); .NET 10 SDK.

---

## 11. Riesgos y mitigaciones

- **PaddleOCR/Python en dev no instalado** → el pre-vuelo lo detecta con mensaje accionable;
  documentar el setup en README.
- **Formato estructurado mal emitido por la IA** → el parser de `generador_estudio_final.py`
  tiene fallback tolerante (prosa → párrafo, etc.).
- **Costo/uso de tokens en el run real** → validar key+conexión antes de gastar; registrar `usage`.
- **DejaVu ausente** → fuentes empaquetadas en `runtime/fonts/`, resueltas por ruta relativa.
