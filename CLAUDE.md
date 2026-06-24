# Resúmenes de Estudio — Guía del proyecto

App de escritorio (.NET 9 / WPF) que convierte material de estudio (comercio exterior) en **PDFs de resumen por tema** y permite **simular exámenes** con corrección por IA. Incluye **activación por licencia**. Primera versión vendible: **1.3.0**.

## Idioma
- Respondé y escribí TODO en **español**, con acentos y signos correctos (á, é, í, ó, ú, ñ, ¿, ¡). Los identificadores de código quedan en su forma original.

## Identidad git (IMPORTANTE)
- Este repo commitea con identidad **local** `emmavzmymtec / emmavzmymtec@gmail.com` (NO la global `emmanuelz`). Verificá `git config --get user.email` antes de commitear. No toques la config global.
- Repo **público**: github.com/emmavzmymtec/Resumenes. Antes de pushear, verificá que no haya secretos hardcoded — `ADMIN_KEY` y la clave privada de firma (`FIRMA_PRIVADA_PEM`) viven en Railway, **no** en el código.
- El usuario decide los **merges a main** y los **push**.

## Estructura (ports & adapters)
- `src/Resumenes.Core` — **dominio puro** (`net9.0`, sin dependencias de infraestructura): modelos, interfaces/puertos, lógica pura (`Examenes/DescriptorRespuestas`, `Examenes/EvaluadorRespuesta`, `Licencias/`).
- `src/Resumenes.Infrastructure` — adaptadores (`net9.0`): IA (Deepseek), SQLite, OCR, PDF, exámenes (`GeneradorExamen`, `CorrectorExamen`, `ServicioExamenes`).
- `src/Resumenes.Ui` — app WPF (`net9.0-windows`, MVVM CommunityToolkit + WPF-UI 4.3): `ViewModels/`, `Vistas/` (XAML).
- `src/Resumenes.Cli` — CLI.
- `src/Resumenes.Licencias.Api` — API de licencias (Minimal API + EF Core), desplegada en **Railway** (`https://resumenes-production.up.railway.app`).
- Tests (xUnit): `tests/Resumenes.Tests` (Core/Infra), `tests/Resumenes.Ui.Tests`, `tests/Resumenes.Licencias.Api.Tests`.

## Comandos
- Build: `dotnet build`
- Tests (suite completa): `dotnet test` — **la app WPF debe estar CERRADA**, si no el build de `Resumenes.Ui.Tests` falla por DLLs bloqueadas (`MSB3027`). Ver skill `verificar`.
- Correr la app: `dotnet run --project src/Resumenes.Ui`. **El usuario prefiere probar la UI él mismo**; entregá tras build+tests verdes, no automatices validación visual.
- Release: ver skill `release`.

## Convenciones de código
- **TDD** (skill `superpowers:test-driven-development`): tests reales (cripto/JSON/SQLite reales, fakes de IA), no mocks de la lógica bajo prueba.
- **SQLite**: el esquema (`src/Resumenes.Infrastructure/schema.sql`) usa `CREATE TABLE IF NOT EXISTS`. Columnas nuevas → de forma **aditiva** con `AsegurarColumna(...)` en `SqliteRepositorioEstado.InicializarEsquema` (el `ALTER ADD COLUMN` no admite `CHECK`). No tocar `schema_version`.
- **Navegación UI**: el back nativo del `NavigationView` está deshabilitado a propósito; cada subpantalla tiene su botón **"Volver"** context-aware.

## IA (Deepseek)
- Vía `IClienteIA.CompletarAsync(SolicitudIA, ct) → RespuestaIA`.
- `SolicitudIA(sistema, promptUser, temperatura, maxTokens, promptVersion, modelo)` — el texto del usuario es **`PromptUser`** (no `Usuario`).
- `RespuestaIA(Texto, FinishReason, TokensPrompt, TokensCompletion, TokensTotal)` — **5 argumentos**.
- En tests, fakeá `IClienteIA` copiando el patrón de un test existente (ej. `DevolucionIaTests`, `GeneradorExamenFormatoTests`).

## Dominio
- **Simulador de exámenes**: 7 tipos (`McUna`, `McVarias`, `VfJustificado`, `Desarrollo`, `DesarrolloItems`, `Completar`, `Emparejar`). Objetivas se corrigen localmente; abiertas con IA. El `DatosJson` de la pregunta guarda la respuesta correcta/esperada; el `RespuestaJson` del alumno varía por tipo. Estado por puntaje: `EvaluadorRespuesta.Estado` → ≥0.85 Correcta, ≥0.40 Parcial, resto Incorrecta. Devolución general por IA (best-effort) en `CorrectorExamen.GenerarDevolucionAsync`.
- **Licenciamiento**: gate en `App.OnStartup`. HWID = SHA-256 de `MachineGuid`. Token JWT **ES256** firmado por la API (clave privada en Railway), validado en el cliente con clave pública embebida (BCL puro, sin paquete JWT). Revalidación cada 14 días, gracia sin conexión 30 días. Solo HTTP **403** revoca; 5xx cae en gracia (no borra la licencia).

## Gotchas (ya nos mordieron)
- **WPF `<Run Text="{Binding X}">` bindea TwoWay por defecto** → con propiedades de solo lectura rompe al cargar la vista ("TwoWay no puede funcionar en propiedad de solo lectura"); y dentro de un `ItemTemplate` de lista revienta al scrollear ("Run no es un Visual"). En listas usá `TextBlock`+`StringFormat`; fuera de listas, `Mode=OneWay`.
- **Inno Setup** está per-user: `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe` (no en Program Files).
- **gh CLI no se instala** (norma del usuario): el Release de GitHub lo crea el usuario desde la web.
- No instalar herramientas ni tocar PATH/variables del entorno sin permiso explícito.
- En PowerShell, `git`/`dotnet` muestran su stderr en rojo aunque el exit code sea 0 — mirá `$LASTEXITCODE`.

## Workflow
- Para features/cambios no triviales: **superpowers** — `brainstorming` → `writing-plans` → `subagent-driven-development` → `finishing-a-development-branch`. Specs en `docs/superpowers/specs/`, planes en `docs/superpowers/plans/`. El ledger de ejecución (git-ignored) vive en `.superpowers/sdd/progress.md`.
