using Resumenes.Core.Modelos;

namespace Resumenes.Core.Orquestacion;

public record PasoPipeline(
    Etapa Etapa,
    string? ArchivoId,
    string? TemaId,
    string? RutaArtefacto,
    Func<CancellationToken, Task<string>> CalcularHashEntrada,
    Func<ContextoPaso, Task> Ejecutar,
    string? PromptVersion = null,
    string? ModeloIa = null);

public record ResultadoEjecucion(
    int Ok,
    int Salteados,
    int Errores,
    IReadOnlyList<string> MensajesError);
