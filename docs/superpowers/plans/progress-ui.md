# Progreso de ejecución — UI (sin git)

Adaptaciones: sin git (sin commits/worktree); revisiones leen archivos creados; net9.0 backbone, net9.0-windows UI.

| Task | Descripción | Estado |
|------|-------------|--------|
| 1 | ProgresoPaso + ContextoPaso (Core) | ✅ |
| 2 | ListarAnalisis (repo, TDD) | ✅ |
| 3 | Progreso en orquestador + PasoPipeline.Ejecutar(ContextoPaso) | ✅ |
| 4 | Sub-progreso OCR/rasterizado (firmas + report) | ✅ |
| 5 | ServicioAnalisis + mover a Infra/Aplicacion + refactor Cli | ✅ 19/19 tests + regresión OK |
| 6 | Andamiaje Resumenes.Ui (shell WPF-UI 4.3.0) | ✅ build OK |
| 7 | DI + navegación + VM base | ✅ build OK, 19/19 tests |
| 8 | LoaderPensante + MapeoProgreso (TDD) | ✅ 22 tests |
| 9 | ⭐ VistaEjecutando + EjecutandoVm (TDD) | ✅ 22 tests (tiempo: falta timer) |
| 10 | Inicio (historial) + Configurar + flujo + timer | ✅ 23 tests |
| 11 | ConfirmarTemas + Generando + Resultados + flujo completo | ✅ 27 tests |
| 12 | Configuración + Onboarding + arranque condicional | ✅ 27 tests |
| 13 | Integración + verificación | ✅ funcional: flujo cableado, pipeline CORRE desde la UI (demo: 6 unidades Completadas). Verif. visual: shell + Inicio ✅; Ejecutando/Confirmar/Resultados/Config → verificación del usuario (captura limpia bloqueada por foreground/idempotencia) |

UI COMPLETA (funcional): 8 pantallas, flujo Inicio→Configurar→Ejecutando→ConfirmarTemas→Generando→Resultados + Configuración + Onboarding. WPF-UI 4.3.0 Fluent oscuro/claro. 27 tests. Se agregó arg `--demo <carpeta>` en App (afordancia de verificación). Pendiente: review final + sync de specs + tests de integración → sub-proyecto PULIDO. Próximo sub-proyecto: INSTALADOR (bootstrapper).

Nota: Tasks 1-5 se ejecutan como un bloque "refactor backbone" (acopladas por el cambio de firma).

Fix (post Task 10): el NavigationView de WPF-UI no usaba DI → DataContext nulo en las páginas (historial vacío). Resuelto con `ProveedorPaginasDi : INavigationViewPageProvider` + `SetPageProviderService`. Inicio verificado visualmente (4 análisis en tarjetas).

Fix (crash al pulsar "Nuevo análisis"): `VistaConfigurar.xaml` tenía `<Page.Resources>` al final del archivo, pero la línea del `MensajeError` usaba `{StaticResource StringToVisConverter}` antes en orden de documento → *forward reference* de StaticResource → `XamlParseException` al navegar (compila, falla en runtime). Era la única vista con el recurso al final; las otras 8 lo declaran arriba. Resuelto moviendo `Page.Resources` al tope y declarando `xmlns:vm` en la raíz (igual que `VistaInicio`). Además se agregó **manejo global de excepciones** en `App.OnStartup` (`DispatcherUnhandledException` + `AppDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException`): registra el detalle en `%LOCALAPPDATA%/ResumenesApp/logs/ui-error.log` y muestra un diálogo amigable en vez de cerrarse en silencio ("nunca un crash mudo" del diseño). Verificado: lanzada la app e invocado "Nuevo análisis" vía UI Automation → la app sobrevive y Configurar renderiza correctamente (selector de carpeta, archivos, prompt, botón Analizar).

## Ronda de bugs reportados por el usuario (diagnóstico con workflow + verificación visual UI Automation)

Diagnóstico exhaustivo (5 áreas × investigador + verificador adversarial). Fixes aplicados y verificados:

1. **Scroll con rueda no funcionaba sobre el contenido** → class handler global `ScrollViewer.PreviewMouseWheel` en `App.OnStartup` (`EnRuedaScrollViewer`): elige, desde el cursor hacia afuera, el primer ScrollViewer que pueda desplazarse y lo desplaza; respeta SVs internos (lista, log, textbox). **Verificado** con rueda simulada (SendInput WHEEL): la página scrollea sobre las tarjetas.
2. **DPI de rasterizado sin valor / control roto** → NO era el binding int↔double? (funciona); era la columna de **80px** que aplastaba el `ui:NumberBox` con spin buttons. Ensanchada a 150px + `MaxDecimalPlaces=0` + `SpinButtonPlacementMode=Compact`. **Verificado**: muestra "200".
3. **Loader "medialuna"** → era el `ui:ProgressRing` determinado a Progress≈0 (arco), redundante con LoaderPensante. Quitado de `VistaEjecutando` y `VistaGenerando`; LoaderPensante siempre visible. **Verificado**: solo los 3 puntos.
4. **Contraste de los pasos del stepper** → estilos `CirculoPasoBase`/`EtiquetaPasoBase`/`ConectorBase` con borde visible, número en `TextFillColorPrimaryBrush`, estado **completado** (verde + ✓) vía flags `Paso1Hecho`/`Paso2Hecho`. **Verificado**.
5. **No se veía avance / pantalla atascada** → causa raíz REAL (más profunda que la diagnosticada): el pipeline cacheado resolvía **sincrónicamente** dentro de la navegación que creaba la página, por lo que la navegación a la siguiente pantalla era **re-entrante** y el NavigationView la descartaba. Fix: `await Task.Yield()` al inicio de `EjecutarAsync`/`GenerarAsync`. Además se agregó progreso macro monótono (`FraccionGlobal` + `TextoItem` "Archivo i de N").
6. **`DataContext` transitorio rompía los bindings de TODAS las pantallas con parámetro** (Ejecutando en blanco, ConfirmarTemas vacío, `QuitarCommand` recibía `TemaDetectado`): WPF-UI fijaba el parámetro como DataContext y el reset al VM dejaba los bindings pegados. **Refactor robusto**: `ServicioNavegacion` ya NO pasa el parámetro como DataContext; lo guarda y la página lo consume en `Loaded` (`ConsumirParametro`), manteniendo el VM como DataContext desde el inicio. Las 4 vistas con parámetro (Ejecutando/ConfirmarTemas/Generando/Resultados) reescritas con este patrón. `ParametroTemas.Temas` → `TemasDetectados` para evitar colisión de nombres.
7. **Continuar** no reanudaba (no llamaba `AbrirOCrearAsync`, servicio stateful sin archivos) → `InicioVm.Continuar` async: `AbrirOCrearAsync(carpeta)` + navega a Ejecutando (pipeline idempotente reanuda). Guarda si la carpeta no existe.
8. **Eliminar** era stub → `IRepositorioEstado.EliminarAnalisis(id)` + `SqliteRepositorioEstado` (DELETE en cascada) + `InicioVm.Eliminar` con confirmación (MessageBox) y quita de la colección.
9. **Exportar/Abrir carpeta no respondían** → `Exportar` deshabilitado porque `CanExecute` no se reevaluaba al poblar `Pdfs` (suscripción `CollectionChanged`); `RutaSalida` ahora absoluta (`Path.GetFullPath`) y workspace resuelto a ruta absoluta en `App` (evita depender del CWD). Mismo fix de `NotifyCanExecuteChanged` para `GenerarCommand`/`FusionarCommand` en ConfirmarTemas.

**Verificación e2e (UI Automation)**: flujo completo Inicio → Continuar → ConfirmarTemas (tema renderiza) → Generar → Generando → **Resultados** (1 PDF "Comercio_exterior_y_logística" 32 KB, ruta absoluta, botones Abrir/Exportar habilitados), sin diálogos de error. Scroll, DPI, loader y stepper verificados por captura. Build 0 errores, 27/27 tests.
