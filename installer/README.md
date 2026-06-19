# Publicar el instalador

## 1. Publicar la app
dotnet publish src/Resumenes.Ui/Resumenes.Ui.csproj -c Release -r win-x64 --self-contained false -o publish/app

## 2. Armar bundles (una vez por versión)
- Preparar Python portable en %TEMP%\pyenv\python (python-build-standalone + pip install pymupdf paddleocr paddlepaddle fpdf2).
- Correr OCR una vez para poblar %USERPROFILE%\.paddlex\official_models.
- powershell -File installer\build-bundles.ps1 -BaseUrl "<URL-de-tus-releases>"
  → genera dist\bundles\{python-env,libreoffice,paddle-models}.zip + manifest.json

## 3. Subir
- Subí los 3 .zip al host (GitHub Releases recomendado; Drive posible con link directo + cuidado con cuota/interstitial).
- Subí manifest.json (o servilo desde el mismo host). Copiá su URL.

## 4. Configurar y compilar el instalador
- Pegá la URL del manifest en config\settings.instalacion.json (campo ManifestUrl).
- "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\Resumenes.iss  → dist\ResumenesSetup.exe

## 5. Probar en máquina/usuario limpio
- Ejecutar ResumenesSetup.exe (instala app + .NET si falta).
- Abrir la app → Onboarding → "Descargar dependencias" → progreso → al terminar, procesar un PDF de prueba.
