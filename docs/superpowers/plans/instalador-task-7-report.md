# Task 7 Report — Inno Setup + Publish

**Fecha:** 2026-06-18  
**Estado:** COMPLETA (salvo compilación del instalador: paso manual)

## Archivos creados

| Archivo | Estado |
|---|---|
| `config/settings.instalacion.json` | Creado — exacto al plan, con `ManifestUrl` placeholder y rutas por-usuario con variables de entorno |
| `installer/Resumenes.iss` | Creado — exacto al plan: instalación por-usuario, [Files] con app + scripts + fonts + settings, [Icons], [Tasks], [Run], [Code] con detección/descarga del runtime .NET 9 |

## Publish

`dotnet publish src/Resumenes.Ui/Resumenes.Ui.csproj -c Release -r win-x64 --self-contained false -o publish/app --nologo`  
→ **OK.** `publish/app/Resumenes.Ui.exe` generado (0.14 MB, framework-dependent, sin runtime .NET embebido). 23 archivos en `publish/app/`.  
Solo warnings preexistentes (CA1416 DPAPI, CS0618 SetDialogHost); 0 errores.

## Compilación del instalador

**ISCC.exe NO encontrado** en `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`.  
Paso manual para el usuario:
1. Instalar **Inno Setup 6** desde https://jrsoftware.org/isdl.php
2. Correr: `& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\Resumenes.iss`
3. Se generará `dist\ResumenesSetup.exe` (estimado < 15 MB, depende del contenido de `publish/app`).

## Auto-review

- `publish/app` es framework-dependent (Resumenes.Ui.exe = 0.14 MB, sin runtime embebido): **OK**
- `.iss` referencia `..\publish\app\*`, `..\runtime\scripts\*`, `..\runtime\fonts\*`, `..\config\settings.instalacion.json`: **OK**
- `settings.instalacion.json` tiene `ManifestUrl` y todas las rutas por-usuario con `%LOCALAPPDATA%`/`%USERPROFILE%`: **OK**

## Inquietudes

- `runtime\scripts` y `runtime\fonts` deben existir en la máquina antes de correr ISCC; si están vacíos Inno Setup fallará en las entradas `[Files]` correspondientes. Inno Setup acepta `Flags: ignoreversion` pero **no** `Flags: skipifsourcedoesntexist` por defecto — si alguna de esas carpetas no existe, habrá un error de compilación del `.iss`. El usuario debe asegurarse de que existan (aunque estén vacías, o agregar el flag correspondiente si no hay contenido todavía).
- `ManifestUrl` en `settings.instalacion.json` queda como placeholder `"PEGAR_URL_DEL_MANIFEST_AQUI"`: se debe editar antes de compilar el instalador final.
