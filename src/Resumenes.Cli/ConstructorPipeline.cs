// ConstructorPipeline y Prompts se movieron a Resumenes.Infrastructure.Aplicacion.
// Este alias permite que Program.cs siga compilando sin cambios de namespace.
global using ConstructorPipeline = Resumenes.Infrastructure.Aplicacion.ConstructorPipeline;
global using Prompts = Resumenes.Infrastructure.Aplicacion.Prompts;
