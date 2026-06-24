# Mejoras al simulador de exámenes — Diseño

> **Fecha:** 2026-06-24
> **Estado:** Aprobado (diseño). Pendiente: plan de implementación (`writing-plans`).
> **Alcance:** una sola spec, 6 mejoras al simulador existente, implementación por fases.

## Goal

Que al terminar un examen el estudiante **aprenda del resultado**: ver la respuesta
correcta de cada pregunta (esté bien o mal), recibir una **devolución de IA** que
señale en qué profundizar, y que la corrección y la experiencia de rendir sean más
justas y completas. Incluye completar dos cosas que hoy quedaron a medias
("Marcar para revisar") o flojas (UX de "Emparejar").

## Estado actual (relevante)

- La pantalla de resultados (`VistaResultadoExamen` + `ResultadoExamenVm`) muestra nota,
  %, aprobado, un texto fijo *"Acertaste N de M"* y, por pregunta, el feedback de IA
  (solo en abiertas). **No muestra ni la respuesta del alumno ni la correcta.**
- Las **objetivas** ya guardan la respuesta correcta en `DatosJson` (opciones `correcta`,
  `respuestas`, `pares`, `esVerdadero`). Las **abiertas** solo guardan criterios/guía, no
  una respuesta redactada.
- La corrección marca `Correcta` solo con puntaje **máximo** (todo o nada).
- *"Marcar para revisar"* tiene checkbox pero **no persiste, no se restaura, no hay
  mini-mapa ni salto** → decorativo.
- *"Emparejar"* funciona, pero la UI deja elegir la **misma opción de la derecha** para
  dos de la izquierda, y al fallar no muestra el emparejamiento correcto.

## Decisiones cerradas

| Tema | Decisión |
|---|---|
| Respuesta correcta de abiertas | Generar **al armar el examen** (respuesta modelo breve en desarrollo/ítems; justificación en V/F) y guardarla en `DatosJson`. |
| Pantalla de resultados | Mostrar **tu respuesta + la correcta + estado + feedback** por pregunta. |
| Corrección | Tres niveles por puntaje: **Correcta ≥85%**, **Parcial ≥40%**, **Incorrecta <40%**; prompt de corrección más justo. |
| Devolución de IA | **Automática** al corregir (1 llamada extra de IA); mensaje breve y motivador que señala temas flojos. La IA **infiere los temas** del contenido. |
| Marcar para revisar | **Completarlo**: persistir, restaurar, mini-mapa de preguntas con estado y salto. |
| Emparejar | UX al rendir: **no permitir elegir dos veces la misma** opción de la derecha. |

---

## 1. Respuesta esperada en preguntas abiertas (generación)

`GeneradorExamen` ya pide las preguntas a la IA con un formato JSON por tipo. Se amplía
el formato de las abiertas para que la IA devuelva también la respuesta esperada, que se
guarda en `DatosJson`:

- **Desarrollo:** agregar `"respuestaEsperada": "<respuesta modelo breve, 2-4 frases>"`.
- **DesarrolloItems:** cada ítem suma `"respuestaEsperada": "<breve>"`.
- **VfJustificado:** agregar `"justificacion": "<por qué es V/F>"` (ya tiene `esVerdadero`).

El **corrector no cambia** por esto (sigue usando `esVerdadero`/criterios para puntuar);
estos campos son para **mostrarlos** en resultados. Costo: algo más de tokens de salida
en la **generación** (una vez por examen), no en cada corrección.

## 2. Pantalla de resultados enriquecida

Un **helper puro** `DescriptorRespuestas` (en Core) toma `(PreguntaExamen, RespuestaUsuario?)`
y produce dos textos legibles según el tipo:

- **`RespuestaUsuario`** (lo que respondió) y **`RespuestaCorrecta`** (lo esperado):
  - McUna/McVarias → texto de la(s) opción(es) elegida(s) / correcta(s).
  - Completar → palabras dadas / esperadas.
  - Emparejar → pares "izquierda → derecha" elegidos / correctos.
  - VfJustificado → "Verdadero/Falso" (+ justificación del alumno) / V-F correcto + `justificacion`.
  - Desarrollo / DesarrolloItems → el texto del alumno / la `respuestaEsperada`.

`ItemResultadoVm` suma `RespuestaUsuario`, `RespuestaCorrecta` y `Estado` (ver §3).
`VistaResultadoExamen.xaml` por pregunta muestra: enunciado, **tu respuesta**,
**respuesta correcta**, el **estado** (chip de color) y el **feedback** de IA si lo hay.

## 3. Corrección menos estricta (tres niveles)

- Se introduce `EstadoRespuesta { Correcta, Parcial, Incorrecta }`, **derivado del puntaje**:
  `obtenido/puntos ≥ 0.85` → Correcta; `≥ 0.40` → Parcial; resto → Incorrecta.
  - Las objetivas siguen siendo 0 o máximo → caen en Correcta/Incorrecta (sin Parcial).
  - Las abiertas (puntaje continuo de la IA) pueden quedar Parcial.
- `RespuestaUsuario.Correcta` (bool) se mantiene para los cálculos; el **estado mostrado**
  se deriva del porcentaje en el VM (no rompe el cálculo de nota).
- El prompt de `CorregirAbiertasAsync` se ajusta para **reconocer lo parcialmente correcto**
  y no exigir perfección ("asigná puntaje proporcional a lo correcto; no penalices de más").

## 4. Devolución general por IA (automática)

Al corregir, después de `CalcularResultado`, se hace **una llamada a la IA** con un resumen
del examen (por pregunta: enunciado breve + estado/puntaje) pidiendo un mensaje **breve
(2-4 frases), motivador**, que reconozca lo bueno y señale **1-2 temas/conceptos a
profundizar**. La IA **infiere los temas** del contenido de los enunciados (no requiere
mapear ids de temas).

- Nuevo método en `ICorrectorExamen`: `Task<(string texto, int tokIn, int tokOut)>
  GenerarDevolucionAsync(IReadOnlyList<(PreguntaExamen p, RespuestaUsuario r)> todo, double pct, string modelo, CancellationToken ct)`.
- `ServicioExamenes` lo invoca en el flujo de corrección, guarda el texto en
  `Examen.FeedbackGeneral` y acumula los tokens/costo.
- **Reemplaza** el "Acertaste N de M" (el % de acierto ya se muestra aparte). Si la llamada
  falla (sin internet / error), se cae a un texto fijo de respaldo (no rompe la corrección).

## 5. "Marcar para revisar" funcional

- **Persistencia:** se agrega `RespuestaUsuario.MarcadaRevisar` (bool) + columna en SQLite
  (migración aditiva con `AsegurarColumna`, como las demás). `GuardarActual` la persiste;
  `CargarInterno` la restaura al repoblar la UI.
- **Mini-mapa:** un panel de "dots" (uno por pregunta) en `VistaRendirExamen`, con estado
  visual: **actual / respondida / marcada / sin responder**, y click para **saltar** a esa
  pregunta. `RendirExamenVm` expone la lista de items (índice + estado) y un comando
  `IrAPregunta(indice)`.

## 6. Emparejar — UX al rendir

En `PreguntaRendirVm`/`VistaRendirExamen`, los ComboBox de la derecha excluyen las opciones
**ya elegidas** por otras filas (o las deshabilitan), evitando duplicados. La validación de
corrección no cambia; esto solo mejora la experiencia. La respuesta correcta se muestra en
resultados (§2).

---

## Componentes afectados

| Archivo | Cambio |
|---|---|
| `Core/Modelos/Examenes.cs` | `RespuestaUsuario.MarcadaRevisar`; `enum EstadoRespuesta`. |
| `Core/Interfaces/ICorrectorExamen.cs` | `GenerarDevolucionAsync`. |
| `Core/Examenes/DescriptorRespuestas.cs` (nuevo) | Helper puro: respuesta legible por tipo. |
| `Infrastructure/Examenes/GeneradorExamen.cs` | Respuesta esperada en el formato de abiertas. |
| `Infrastructure/Examenes/CorrectorExamen.cs` | Devolución IA + prompt de corrección más justo. |
| `Infrastructure/Examenes/ServicioExamenes.cs` | Orquestar la devolución al corregir. |
| `Infrastructure/Persistencia/*` (schema + repo) | Columna `MarcadaRevisar` (guardar/leer). |
| `Ui/ViewModels/ResultadoExamenVm.cs` | `ItemResultadoVm`: respuestas legibles + estado. |
| `Ui/ViewModels/RendirExamenVm.cs` | Marcar para revisar (persistir/restaurar) + mini-mapa + saltar. |
| `Ui/Vistas/VistaResultadoExamen.xaml` | Mostrar tu respuesta + correcta + estado por pregunta. |
| `Ui/Vistas/VistaRendirExamen.xaml` | Mini-mapa de dots + ComboBox de emparejar sin duplicados. |

## Testing

- **Core puro:** `DescriptorRespuestas` por los 7 tipos (legible correcto de respuesta y
  esperada); derivación de `EstadoRespuesta` por umbrales (0.85 / 0.40, fronteras).
- **Corrección:** la devolución IA con un fake de `IClienteIA` (texto devuelto, fallback ante
  error); el prompt de corrección sigue puntuando bien las objetivas.
- **Generación:** parseo de `respuestaEsperada`/`justificacion` desde el JSON de la IA (con
  un fake de IA), tolerando que falten (no rompe).
- **Ui:** `RendirExamenVm` (marcar/restaurar; estados del mini-mapa; saltar); emparejar sin
  duplicados; `ItemResultadoVm` arma los textos esperados.

## Fases

A. **Generación con respuesta esperada** (GeneradorExamen + formato JSON + parseo).
B. **Corrección 3-niveles + devolución IA** (CorrectorExamen + ServicioExamenes + prompts).
C. **Resultados enriquecida** (DescriptorRespuestas + ItemResultadoVm + VistaResultadoExamen).
D. **Marcar para revisar + mini-mapa** (modelo + schema + RendirExamenVm + VistaRendirExamen).
E. **Emparejar UX** (PreguntaRendirVm + VistaRendirExamen).

## Fuera de alcance (backlog)

- Generar respuesta modelo para abiertas de exámenes **ya creados** (solo aplica a nuevos).
- Issues menores de robustez detectados en la auditoría (GroupName "VF" global; `McUna`
  serializa `"null"` string; reanudar respuestas desde otra sesión) — no afectan el uso normal.
