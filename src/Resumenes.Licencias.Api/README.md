# API de Licencias

Minimal API (.NET 9) para activar/validar/administrar licencias de la app Resúmenes.
Esquema creado con `EnsureCreated()`. Postgres en Railway, SQLite en local.

## Variables de entorno

| Variable | Para qué | Dónde |
|---|---|---|
| `FIRMA_PRIVADA_PEM` | Clave privada EC (P-256) para firmar tokens | Railway (secreto) |
| `ADMIN_KEY` | Secreto de los endpoints `/admin/*` | Railway (secreto) |
| `DATABASE_URL` | Postgres (la inyecta Railway al sumar el plugin) | Railway |
| `PORT` | Puerto de escucha (lo inyecta Railway) | Railway |

Sin `DATABASE_URL` la API usa SQLite (`licencias.db`) — útil para correr local.

## Generar el par de claves (una sola vez)

    dotnet run --project src/Resumenes.Licencias.Api -- gen-keys

Copiá el bloque **privado** a `FIRMA_PRIVADA_PEM` en Railway. Guardá el bloque
**público**: se embebe en el cliente (fase futura). NO commitear ninguna de las dos.

## Despliegue en Railway

1. Nuevo servicio → "Deploy from repo" apuntando a este subdirectorio (o Root
   Directory = `src/Resumenes.Licencias.Api`). Railway detecta el `Dockerfile`.
2. Agregar el plugin **PostgreSQL** → setea `DATABASE_URL` automáticamente.
3. Cargar variables `FIRMA_PRIVADA_PEM` y `ADMIN_KEY`.
4. Deploy. Probar `GET https://<tu-dominio>.railway.app/salud` → `ok`.

## Correr local

    dotnet run --project src/Resumenes.Licencias.Api
    # usa SQLite; setear ADMIN_KEY y FIRMA_PRIVADA_PEM en el entorno primero
