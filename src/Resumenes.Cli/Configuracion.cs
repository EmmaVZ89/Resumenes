// Configuracion se movió a Resumenes.Infrastructure.Aplicacion para ser compartida con ServicioAnalisis.
// Este alias permite que Program.cs y el resto del Cli sigan compilando sin cambios de using.
global using Configuracion = Resumenes.Infrastructure.Aplicacion.Configuracion;
