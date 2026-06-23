# Licenciamiento — bloqueo de la app por clave de activación

> **Fecha:** 2026-06-23
> **Estado:** Diseño propuesto. Pendiente: revisión del usuario → plan de implementación (`writing-plans`).
> **Alcance:** una sola spec; implementación por fases (servidor → cliente → integración).

## Goal

Convertir la app en un producto **de pago**: que esté **bloqueada hasta ingresar una
clave de activación válida**, con capacidad de **ver y limitar las máquinas** por
licencia y de **revocar** una clave cuando haga falta. Pago **único / perpetuo**.

## No-objetivos (fuera de alcance)

- Cobro/pasarela de pago automatizada (Mercado Pago, Stripe, etc.): la venta y el envío
  de la clave son **manuales** por ahora (vos cobrás y mandás la clave).
- Suscripción con vencimiento: el modelo es **perpetuo** (el sistema queda preparado para
  sumar vencimiento después, pero no se implementa ahora — YAGNI).
- Ofuscación / anti-tamper del binario: **opcional y futura**. Ver "Modelo de amenaza".
- Planes/features diferenciados por licencia (básico vs premium): no ahora.

## Principios que se mantienen (de la app actual)

- **Puertos y adaptadores:** lógica pura testeable en `Resumenes.Core`; adaptadores
  (Windows, HTTP, DPAPI) en `Resumenes.Ui`; UI MVVM con WPF-UI.
- **Windows-only, por usuario:** datos en `%LOCALAPPDATA%/ResumenesApp/`.
- **Mínima fricción:** después de activar, la app funciona **offline** salvo una
  revalidación esporádica.
- **Testing:** la lógica de decisión (validar token, estado de licencia, gracia) es pura
  y se prueba con xUnit sin red ni Windows.

---

## 1. Decisiones cerradas

| Decisión | Elección |
|---|---|
| Objetivo | Monetización / venta |
| Infraestructura | Servidor propio en **Railway** (.NET Minimal API + Postgres) |
| Anti-compartir | Límite de **máquinas por clave** (atadas al ID de equipo / HWID) |
| Validación | Activación online + **revalidación periódica** con gracia offline |
| Revocación | **Sí** (cortable desde el servidor en cualquier momento) |
| Cobro | **Pago único / perpetuo** (sin vencimiento) |
| Bloqueo | **Toda la app** bloqueada hasta activar |
| Máquinas por licencia (default) | **2** (configurable por licencia) |
| Revalidación / gracia | Revalida cada **14 días** / gracia offline de **30 días** |

---

## 2. Arquitectura

Dos piezas nuevas + un "gate" en el arranque de la app actual:

```
┌─────────────────────────────┐         ┌──────────────────────────────┐
│  App WPF (cliente)          │  HTTPS  │  API de Licencias (Railway)  │
│                             │ ──────► │  .NET Minimal API + Postgres │
│  • Gate de arranque         │ activar │                              │
│  • ServicioLicencia         │ validar │  • POST /activar  /validar   │
│  • ServicioHwid             │ ◄────── │  • /admin/... (solo vos)     │
│  • Token firmado (DPAPI)    │  JWT    │  • Firma con clave privada   │
│  • Clave PÚBLICA embebida   │ firmado │  • Postgres: licencias +     │
│  • VistaActivacion (UI)     │         │    activaciones              │
└─────────────────────────────┘         └──────────────────────────────┘
```

**Núcleo de seguridad:** la app **no decide sola** si una clave es válida. El servidor
emite un **JWT firmado con su clave privada (ES256, curva EC P-256)**; la app lo valida
con la **clave pública embebida**. Un token manipulado no pasa la verificación de firma
sin la clave privada. El token se guarda **cifrado con DPAPI** (ámbito CurrentUser), por
lo que no se puede copiar a otra PC ni a otro usuario de Windows.

### Estructura de proyectos

| Proyecto | Rol | Nuevo? |
|---|---|---|
| `src/Resumenes.Core` | `Licencias/`: contratos (DTOs), `ValidadorTokenLicencia` (valida firma + claims con clave pública), `EvaluadorEstadoLicencia` (decisión pura de estado). Sin WPF, sin red, sin Windows. | Amplía existente |
| `src/Resumenes.Ui` | `Servicios/Licencia/`: `ServicioHwid`, `AlmacenLicencia` (DPAPI), `ClienteLicenciasHttp`, `ServicioLicencia` (orquesta). `Vistas/VistaActivacion.xaml` + `ViewModels/ActivacionVm`. Gate en `App`/`MainWindow`. | Amplía existente |
| `src/Resumenes.Licencias.Api` | Minimal API que se despliega en Railway. **No** se distribuye con la app ni se incluye en el instalador. | **Nuevo** |
| `tests/Resumenes.Core.Tests` | Tests de `ValidadorTokenLicencia` y `EvaluadorEstadoLicencia`. | Amplía/crea |
| `tests/Resumenes.Ui.Tests` | Tests de `ServicioLicencia` con cliente HTTP y almacén mockeados. | Amplía existente |

> El JWT usa la librería estándar de .NET (`Microsoft.IdentityModel.JsonWebTokens` /
> `System.Security.Cryptography` para EC). No hace falta un proyecto compartido entre app
> y API: solo comparten el **formato de claims** y el **algoritmo** (documentados acá).

---

## 3. Servidor — API de Licencias (Railway)

### 3.1 Modelo de datos (Postgres)

**`licencias`**
- `id` (uuid, PK)
- `clave` (text, única, indexada) — el código que recibe el comprador
- `comprador` (text), `email` (text)
- `max_maquinas` (int, default 2)
- `estado` (text: `activa` | `revocada`, default `activa`)
- `creada_en` (timestamptz), `notas` (text, nullable)

**`activaciones`**
- `id` (uuid, PK)
- `licencia_id` (uuid, FK → licencias)
- `hwid` (text) — ID de equipo hasheado que manda la app
- `nombre_equipo` (text) — para que lo reconozcas en el panel
- `primera_activacion` (timestamptz), `ultima_validacion` (timestamptz)
- Único `(licencia_id, hwid)` — reactivar la misma máquina no consume un asiento nuevo.

### 3.2 Endpoints públicos (los llama la app)

- **`POST /activar`** `{ clave, hwid, nombreEquipo }`
  - Reglas: la clave existe **y** está `activa` **y**
    `count(activaciones de la clave) < max_maquinas` **o** ese `hwid` ya estaba registrado.
  - Éxito → registra/actualiza la activación y devuelve **`{ token }`** (JWT firmado con
    claims `lic` (licencia_id), `hwid`, `sub` (comprador), `iat`).
  - Error → `{ error: "clave_invalida" | "revocada" | "limite_alcanzado" }` con HTTP 4xx.
- **`POST /validar`** `{ licencia_id, hwid }`
  - Chequea el estado **actual** de la licencia; si sigue `activa` y el `hwid` está
    registrado → `{ estado: "activa" }` y actualiza `ultima_validacion`.
  - Si fue revocada o el hwid ya no está → `{ estado: "revocada" }` (HTTP 403).

### 3.3 Endpoints de administración (solo vos)

Protegidos con un header `X-Admin-Key` comparado (en tiempo constante) contra un secreto
en variable de entorno de Railway.

- `POST /admin/licencias` `{ comprador, email, maxMaquinas? }` → crea licencia, **genera la
  clave** y la devuelve para que se la mandes al comprador.
- `GET /admin/licencias` → lista licencias con sus activaciones (acá **ves las máquinas**).
- `POST /admin/licencias/{id}/revocar` → pone `estado = revocada`.
- `DELETE /admin/activaciones/{id}` → **libera un asiento** (cambio de PC / reinstalación).

> Para empezar, el "panel" puede ser estos endpoints + una colección de requests
> (REST Client / `.http`). Un panel web mínimo es una mejora futura opcional.

### 3.4 Secretos y transporte

- **Clave privada de firma (EC P-256):** variable de entorno en Railway. Nunca en el repo.
- **Admin key:** variable de entorno en Railway.
- **`DATABASE_URL`:** la inyecta Railway (Postgres).
- **HTTPS:** lo provee Railway. La app **rechaza** llamar por HTTP plano.
- **Rate limiting** básico en `/activar` y `/validar` para frenar fuerza bruta de claves.

### 3.5 Formato de la clave

Código **opaco** legible/copiable, p. ej. `RESU-XXXXX-XXXXX-XXXXX-XXXXX` (grupos
Crockford base32, sin caracteres ambiguos). La clave **no** lleva criptografía en sí
misma: es un secreto que el servidor busca en la base. La seguridad real está en la
**firma del token** que el servidor devuelve.

---

## 4. Cliente — capa de licencia en la app WPF

### 4.1 Componentes

- **`ServicioHwid`** — calcula un "ID de equipo" estable y lo hashea (SHA-256):
  - Fuente principal: `MachineGuid`
    (`HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid`), muy estable, no requiere admin.
  - Fuente secundaria: serial del volumen del sistema.
  - **No** se usa la MAC (cambia con VPNs/adaptadores virtuales).
- **`AlmacenLicencia`** — guarda/lee el token **cifrado con DPAPI (CurrentUser)** en
  `%LOCALAPPDATA%\ResumenesApp\licencia.dat`.
- **`ClienteLicenciasHttp`** — `HttpClient` propio (timeout corto) contra la API; envuelve
  todo en try/catch y traduce a un resultado tipado (sin tirar excepciones a la UI).
- **`ServicioLicencia`** — orquesta: `Activar(clave)`, `ObtenerEstadoAsync()`,
  revalidación periódica. Toma `ServicioHwid`, `AlmacenLicencia`, `ClienteLicenciasHttp`,
  `ValidadorTokenLicencia` y `EvaluadorEstadoLicencia`.
- **`ValidadorTokenLicencia`** (en Core) — valida firma ES256 con la **clave pública
  embebida** + que `hwid` del token == hwid actual. Puro, testeable.
- **`EvaluadorEstadoLicencia`** (en Core) — función pura: dado `(tieneToken,
  ultimaValidacionExitosa, ahora)` decide
  `SinLicencia | Activa | TocaRevalidar | EnGracia | Bloqueada`.

### 4.2 UI

- **`VistaActivacion` + `ActivacionVm`** — pantalla de bloqueo (mismo estilo WPF-UI):
  - Campo para pegar la clave; muestra el **ID de equipo** (informativo).
  - Botón **Activar** con indicador de carga; estados: activando / error (mensaje del
    servidor) / activada.
  - Si no hay internet al activar → mensaje claro ("se necesita conexión para activar").
- **Gate de arranque** — en `App.OnStartup` (o el primer frame de `MainWindow`):
  `ServicioLicencia.ObtenerEstadoAsync()` decide si se muestra la app o `VistaActivacion`.
  El resto de la navegación no se habilita hasta que el estado sea `Activa`/`EnGracia`.

### 4.3 Embebido de la clave pública

La **clave pública** (no secreta) se incrusta como recurso/constante en `Resumenes.Core`.
La URL base de la API va en config (`settings.instalacion.json` o constante de build),
apuntando al dominio de Railway por HTTPS.

---

## 5. Flujos

### 5.1 Activación (una vez por máquina)

1. Comprador paga → creás la licencia (`POST /admin/licencias`) → le mandás la clave.
2. Abre la app → `VistaActivacion` → pega la clave → **Activar**.
3. App calcula el `hwid` → `POST /activar { clave, hwid, nombreEquipo }`.
4. Servidor valida límite y estado → devuelve **token firmado**.
5. App valida la firma + guarda el token cifrado (DPAPI) → **desbloquea**.

### 5.2 Arranque normal

1. `AlmacenLicencia` lee el token; `ValidadorTokenLicencia` valida firma + hwid offline.
2. `EvaluadorEstadoLicencia` mira `ultimaValidacionExitosa`:
   - **> 14 días** → `TocaRevalidar` → `POST /validar`:
     - `activa` → actualiza marca, entra.
     - `revocada` → bloquea, vuelve a `VistaActivacion`.
     - **sin internet** → entra igual si está dentro de la **gracia** (≤ 30 días desde la
       última validación exitosa); pasados 30 días → pide reconectar para seguir.
   - **≤ 14 días** → entra directo, sin red.
3. Sin token o token inválido/corrupto → `VistaActivacion`.

### 5.3 Casos borde

- **Token borrado/reinstalación de la app:** vuelve a pedir activación; como el `hwid` ya
  está registrado, **no consume** un asiento nuevo.
- **Cambio de PC / formateo:** el `hwid` cambia → cuenta como máquina nueva. Si llegó al
  límite, el comprador pide liberar un asiento (`DELETE /admin/activaciones/{id}`).
- **Reloj manipulado:** la fuente de verdad de la fecha, con conexión, es el servidor.
  Atrasar el reloj solo provoca revalidar más seguido (inofensivo); adelantarlo mucho
  puede bloquear antes de tiempo (aceptable). La gracia se mide contra
  `ultimaValidacionExitosa` guardada, no contra una fecha de expiración local.

---

## 6. Modelo de amenaza (límites honestos)

- **.NET es decompilable.** Con dnSpy/ILSpy un atacante técnico podría **parchear** el
  binario para saltear el gate. La firma protege contra **falsificar tokens/claves**, no
  contra modificar el `.exe`. El público objetivo (gente de comercio exterior) está
  cubierto; protección perfecta en desktop no existe.
- **DPAPI** impide reutilizar un token activado en otra PC/usuario.
- **HWID spoofing** es posible para un técnico, pero queda fuera del modelo realista.
- **Ofuscación** (p. ej. un ofuscador para .NET) es una mejora **futura opcional** si el
  volumen de venta lo justifica; no se incluye ahora.

---

## 7. Testing

- **Core (puro, sin red/Windows):**
  - `ValidadorTokenLicencia`: token válido pasa; firma alterada falla; hwid distinto falla;
    token mal formado / con claims faltantes falla. (El token de activación **no** lleva
    `exp`: es perpetuo; la frescura se controla por revalidación, no por expiración local.)
  - `EvaluadorEstadoLicencia`: matriz de `(token, ultimaValidacion, ahora)` →
    estado esperado (incluye fronteras 14 d y 30 d).
- **Ui:** `ServicioLicencia` con `ClienteLicenciasHttp` y `AlmacenLicencia` fakeados:
  activación OK guarda token; `limite_alcanzado`/`revocada` no desbloquean; revalidación
  sin internet respeta gracia.
- **Api:** tests de endpoints (activar respeta `max_maquinas`; reactivar mismo hwid no
  suma asiento; revocar hace que `/validar` devuelva `revocada`; admin sin key → 401).

---

## 8. Plan por fases (alto nivel)

1. **Fase A — API en Railway:** proyecto `Resumenes.Licencias.Api`, modelo Postgres,
   endpoints públicos + admin, firma ES256, deploy a Railway, smoke test con `.http`.
2. **Fase B — Core de licencia:** contratos, `ValidadorTokenLicencia`,
   `EvaluadorEstadoLicencia` + tests.
3. **Fase C — Cliente:** `ServicioHwid`, `AlmacenLicencia` (DPAPI), `ClienteLicenciasHttp`,
   `ServicioLicencia` + tests; registro en DI.
4. **Fase D — UI y gate:** `VistaActivacion` + `ActivacionVm`; gate de arranque que
   bloquea la app hasta activar; manejo de estados/errores.
5. **Fase E — Release:** versión nueva, instalador, nota de cambios. (La clave de firma y
   la admin key se configuran como variables de entorno en Railway, no en el repo.)

---

## 9. Preguntas abiertas / a confirmar en implementación

- Dominio final de la API en Railway (para la URL base embebida).
- Texto/branding de la pantalla de activación (cómo se obtiene la clave, contacto).
- Si el "panel" admin arranca como colección `.http` o si querés un panel web mínimo ya.
