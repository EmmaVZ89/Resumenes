# Resúmenes de Estudio

Aplicación de escritorio para **Windows (.NET 9 / WPF)** que convierte una carpeta de
material de estudio (PDF, DOC/DOCX, PPT/PPTX, TXT) en **PDFs de resumen por tema**,
listos para preparar un parcial. Pensada para un estudiante de comercio exterior.

> Herramienta personal, monousuario y de una sola máquina. El objetivo rector es un
> flujo end-to-end robusto: _"que nada falle"_.

![Ícono de la app](src/Resumenes.Ui/Recursos/app.ico)

## Características

- **Ingesta universal por OCR**: rasteriza cada documento y le aplica OCR
  (PaddleOCR PP-OCRv6) para todo formato rasterizable; el TXT se ingiere directo a
  texto plano (sin OCR).
- **Conversión de Office → PDF** (DOC/DOCX/PPT/PPTX/XLS/XLSX) con **LibreOffice
  portable** (aislado de cualquier LibreOffice del sistema); si falla o se cuelga,
  recurre automáticamente a **Microsoft Office** (Word/PowerPoint/Excel vía COM) si
  está instalado.
- **Detección de temas** sobre el material y **un PDF de resumen por cada tema**.
- **Resúmenes con IA** (Deepseek cloud): la IA solo emite **contenido estructurado**
  (marcadores) que un **motor de estilo fijo en Python** (fpdf2 + fuentes DejaVu)
  renderiza. La IA nunca ejecuta código → sin riesgo de ejecutar código de un LLM.
- **Prompts editables**: prompt de detección de temas y prompt de resumen, escribibles
  por el usuario en el momento; opción de **re-procesar con un nuevo prompt** reusando
  la información ya extraída (sin re-OCR).
- **Instalador liviano** que descarga las dependencias pesadas (Python portable,
  LibreOffice, modelos OCR) con **progreso, verificación SHA-256 y reanudación**.
- La carpeta original del usuario queda **intacta**: todo se trabaja en un workspace
  propio en `%LOCALAPPDATA%`.

## Arquitectura

| Proyecto | Responsabilidad |
|---|---|
| `src/Resumenes.Core` | Dominio, interfaces, orquestación. |
| `src/Resumenes.Infrastructure` | Adaptadores (OCR, IA, LibreOffice, PDF), pipeline, prompts, instalador/descargador. |
| `src/Resumenes.Ui` | WPF + WPF-UI (Fluent), MVVM (CommunityToolkit.Mvvm). |
| `src/Resumenes.Cli` | Interfaz de línea de comandos. |
| `tests/` | xUnit (`Resumenes.Tests`, `Resumenes.Ui.Tests`). |

Los componentes pesados (Python, LibreOffice) se invocan como **subprocesos**. El
estado de cada análisis se persiste en SQLite (`schema.sql`).

## Requisitos

- Windows 10/11 x64.
- **.NET 9 Desktop Runtime** (el instalador lo instala si falta).
- Internet para la IA (Deepseek). El OCR funciona offline con modelos locales.

## Instalación (usuario final)

1. Descargar `ResumenesSetup.exe` desde **Releases** e instalar (no requiere admin;
   instala por usuario en `%LOCALAPPDATA%`).
2. Abrir la app → **Onboarding** → _Descargar dependencias_ (Python, LibreOffice,
   modelos OCR) con barra de progreso.
3. Cargar la **API key de Deepseek** en _Configuración_.

## Compilar desde el código

```sh
dotnet build Resumenes.sln -c Release
dotnet test
dotnet run --project src/Resumenes.Ui
```

En desarrollo, la app resuelve los scripts en `runtime/scripts`, las fuentes en
`runtime/fonts`, y las dependencias pesadas según `config/settings.ejemplo.json`.
Las dependencias pesadas (LibreOffice, Python, modelos) **no están en el repo**: se
descargan vía los bundles del instalador (ver abajo).

## Empaquetar y publicar el instalador

Ver [`installer/README.md`](installer/README.md). En resumen:

1. `dotnet publish src/Resumenes.Ui -c Release -r win-x64 --self-contained false -o publish/app`
2. Armar bundles: `installer/build-bundles.ps1` → `dist/bundles/*.zip` + `manifest.json`.
3. Subir los `.zip` + `manifest.json` a un host (este repo usa **GitHub Releases**).
4. Poner la URL del `manifest.json` en `config/settings.instalacion.json` → `ManifestUrl`.
5. Compilar `installer/Resumenes.iss` con **Inno Setup** → `dist/ResumenesSetup.exe`.

## Configuración

- `config/settings.ejemplo.json` — perfil de desarrollo.
- `config/settings.instalacion.json` — perfil de instalación (rutas por-usuario en
  `%LOCALAPPDATA%`, `ManifestUrl` del release).
- La **API key de Deepseek** se guarda cifrada con **DPAPI** en
  `%LOCALAPPDATA%\ResumenesApp\config\deepseek.key`. **Nunca** se versiona.

## Solución de problemas

- **Adobe Acrobat — "Error al abrir el documento. Acceso denegado"** al abrir un PDF
  desde la app: es el *Modo protegido* de Acrobat, que bloquea archivos ubicados en
  `%LOCALAPPDATA%`. Usá **"Exportar PDFs"** (copia a Documentos/Escritorio y abrí desde
  ahí) o desactivá en Acrobat *Preferencias → Seguridad (mejorada) → "Habilitar modo
  protegido al iniciar"*.
- **LibreOffice — "bootstrap.ini está dañado"** al procesar un Office: la PC tiene otra
  instalación de LibreOffice/OpenOffice cuyas variables de entorno (`URE_BOOTSTRAP`,
  `UNO_PATH`) desvían al LibreOffice portable. La app ya las neutraliza al lanzarlo y,
  si aun así falla, recurre al fallback a Microsoft Office.
- **El runtime no se descargó** (faltan Python/LibreOffice/modelos): reabrí el
  **Onboarding** y reintentá *Descargar dependencias*; verifica SHA-256 y reanuda lo
  que falte. Revisá que el antivirus no haya puesto archivos en cuarentena.

## Especificación y diseño

- [`especificacionesV2.txt`](especificacionesV2.txt) — especificación vigente.
- [`docs/superpowers/specs/`](docs/superpowers/specs/) y
  [`docs/superpowers/plans/`](docs/superpowers/plans/) — diseños y planes de implementación.

## Licencia

[MIT](LICENSE).
