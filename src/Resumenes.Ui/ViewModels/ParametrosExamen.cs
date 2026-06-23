using Resumenes.Core.Modelos;

namespace Resumenes.Ui.ViewModels;

public record ParametroExamenes(Analisis An);
public record ParametroCrearExamen(Analisis An);
public record ParametroRendir(string ExamenId, Analisis An);
public record ParametroResultadoExamen(string ExamenId, Analisis An);
