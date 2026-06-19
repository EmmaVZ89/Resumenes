# Arma los bundles, calcula SHA-256/tamaño y genera dist/manifest.json.
# Requiere: python-build-standalone (CPython portable) para el bundle Python.
param(
  [string]$RaizProyecto = (Resolve-Path "$PSScriptRoot\.."),
  [string]$Salida = "$PSScriptRoot\..\dist\bundles",
  [string]$BaseUrl = "REEMPLAZAR_URL"   # ej: https://github.com/usuario/repo/releases/download/v1.0.0
)
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $Salida | Out-Null

function ZipDir($origen, $zip) {
  if (Test-Path $zip) { Remove-Item $zip -Force }
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  # Nota: en PowerShell 5.1 (.NET Framework) las entradas quedan con separador '\' en vez de '/'.
  # No es 100% conforme al estandar ZIP, pero el descargador (ZipFile.ExtractToDirectory en .NET 9 / Windows)
  # las trata correctamente como subcarpetas (verificado empiricamente). Para zips portables, correr bajo PowerShell 7+.
  [System.IO.Compression.ZipFile]::CreateFromDirectory($origen, $zip)
}
function Sha($f) { (Get-FileHash $f -Algorithm SHA256).Hash }

# 1) Python: usar python-build-standalone + pip install (relocatable).
#    Bajar de https://github.com/astral-sh/python-build-standalone/releases
#    (cpython-3.12.*-x86_64-pc-windows-msvc-install_only.tar.gz), extraer a $env:TEMP\pyenv\python,
#    y luego:
#      & "$env:TEMP\pyenv\python\python.exe" -m pip install --upgrade pip
#      & "$env:TEMP\pyenv\python\python.exe" -m pip install pymupdf paddleocr paddlepaddle fpdf2
#    Ese 'python' relocatable es lo que se zipea:
$pyEnv = "$env:TEMP\pyenv\python"
if (-not (Test-Path $pyEnv)) { Write-Host "FALTA preparar $pyEnv (ver comentarios del script)"; }
$zipPy = "$Salida\python-env.zip"
if (Test-Path $pyEnv) { ZipDir $pyEnv $zipPy }

# 2) LibreOffice: zipear runtime/libreoffice actual (ya funcional).
$zipLo = "$Salida\libreoffice.zip"
ZipDir "$RaizProyecto\runtime\libreoffice" $zipLo

# 3) Modelos PaddleOCR (cache que la app usa).
$modelos = "$env:USERPROFILE\.paddlex\official_models"
$zipMo = "$Salida\paddle-models.zip"
if (Test-Path $modelos) { ZipDir $modelos $zipMo } else { Write-Host "FALTA $modelos (corré un OCR una vez para poblarlo)" }

# 4) Generar manifest.json con sha256/bytes reales.
function Entry($id, $zip, $destino, $limpiar = $true) {
  if (-not (Test-Path $zip)) { return $null }
  # limpiarDestino=false en modelos: el destino es el cache global ~\.paddlex; NO debe borrarse (fix C1).
  [ordered]@{ id=$id; url="$BaseUrl/$(Split-Path $zip -Leaf)"; sha256=(Sha $zip); bytes=(Get-Item $zip).Length; destino=$destino; tipo="zip"; limpiarDestino=$limpiar }
}
$bundles = @(
  (Entry "python" $zipPy "python"),
  (Entry "libreoffice" $zipLo "libreoffice"),
  (Entry "modelos" $zipMo "%USERPROFILE%\.paddlex\official_models" $false)
) | Where-Object { $_ -ne $null }
$manifest = [ordered]@{ version="1.0.0"; bundles=$bundles }
[System.IO.File]::WriteAllText("$Salida\manifest.json", ($manifest | ConvertTo-Json -Depth 5), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Listo. Bundles + manifest en $Salida"
Write-Host "Subí los .zip a tu host, pegá la BaseUrl correcta y poné la URL de manifest.json en settings.json (ManifestUrl)."
