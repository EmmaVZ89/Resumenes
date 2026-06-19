# Task 6: VistaOnboarding.xaml — Reporte

## Estado
COMPLETO — 0 errores de compilación.

## Archivos modificados
- `src/Resumenes.Ui/Vistas/VistaOnboarding.xaml`

## Build
`dotnet build src/Resumenes.Ui/Resumenes.Ui.csproj -c Debug --nologo` → **Compilación correcta. 0 Errores. 2 Advertencias** (las warnings son preexistentes: `CS0618` en `MainWindow.xaml.cs`, obsoleto `SetDialogHost(ContentPresenter)`; no son de esta tarea).

## Cambios realizados
1. **BoolToVis ya presente**: `<BooleanToVisibilityConverter x:Key="BoolToVis"/>` estaba en `Page.Resources` (línea 9); no se duplicó.
2. **Botón cableado**: se reemplazó el `<ui:Button>` placeholder (`IsEnabled="False"`, `Appearance="Secondary"`, `ToolTip="Se conecta en el sub-proyecto Instalador"`) por un botón enlazado a `DescargarDependenciasCommand` con `Appearance="Primary"`. Sin `IsEnabled` explícito (el `AsyncRelayCommand` se deshabilita solo).
3. **Área de progreso**: `StackPanel` con `Visibility="{Binding Descargando, Converter={StaticResource BoolToVis}}"`, conteniendo:
   - `ProgressBar` (`Minimum=0`, `Maximum=1`, `Value="{Binding FraccionGlobal}"`, `Height=8`).
   - `TextBlock` con `Text="{Binding TextoProgreso}"`, `Foreground="{DynamicResource TextFillColorSecondaryBrush}"`, `TextWrapping=Wrap`.

## Auto-review
- Binding del comando: `Command="{Binding DescargarDependenciasCommand}"` — correcto.
- Binding de las 3 propiedades: `FraccionGlobal`, `TextoProgreso`, `Descargando` — todos correctos.
- El área de progreso aparece solo cuando `Descargando = true` via `BoolToVis`.
- Compila: sí, 0 errores.

## Inquietudes
- Ninguna bloqueante. Las 2 warnings de `CS0618` son anteriores a esta tarea y no afectan la funcionalidad.
- La descripción del texto introductorio del card fue actualizada levemente (de "se realiza desde el Instalador de la aplicación" a "se realiza automáticamente") para reflejar que ahora el botón sí funciona desde dentro de la app.
