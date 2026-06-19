// DetectorTemas y TemaDetectado se movieron a Resumenes.Infrastructure.Aplicacion y Resumenes.Core.Modelos.
// Este alias permite que Program.cs siga compilando sin cambios de namespace.
global using DetectorTemas = Resumenes.Infrastructure.Aplicacion.DetectorTemas;
global using TemaDetectado = Resumenes.Core.Modelos.TemaDetectado;
