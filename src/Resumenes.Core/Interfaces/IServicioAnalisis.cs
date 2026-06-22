using Resumenes.Core.Modelos;
using Resumenes.Core.Orquestacion;

namespace Resumenes.Core.Interfaces;

public record ResultadoLote(int Ok, int Error, IReadOnlyList<string> Fallos);

public interface IServicioAnalisis
{
    Task<Analisis> AbrirOCrearAsync(string carpeta, CancellationToken ct, IReadOnlyCollection<string>? rutasExcluidas = null);
    /// <summary>Nombres de archivo (nivel superior, extensiones aceptadas) candidatos a procesar.</summary>
    IReadOnlyList<string> ListarArchivosCandidatos(string carpeta);
    Task<ResultadoLote> ProcesarArchivosAsync(Analisis an, IProgress<ProgresoPaso>? progreso, CancellationToken ct);
    Task<IReadOnlyList<TemaDetectado>> DetectarTemasAsync(Analisis an, string promptTemas, CancellationToken ct);
    Task<ResultadoLote> GenerarPorTemasAsync(Analisis an, IReadOnlyList<TemaDetectado> temas, string promptResumen, IProgress<ProgresoPaso>? progreso, CancellationToken ct);
}
