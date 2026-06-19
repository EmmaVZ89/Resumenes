namespace Resumenes.Core.Modelos;

public record Analisis(
    string Id,
    string Nombre,
    string CarpetaOrigen,
    string Fingerprint,
    EstadoAnalisis Estado,
    DateTime CreadoEn,
    DateTime ActualizadoEn);

public record Archivo(
    string Id,
    string AnalisisId,
    string NombreOriginal,
    string RutaRelativa,
    string HashSha256,
    long TamanoBytes,
    TipoArchivo Tipo,
    int? Paginas,
    DateTime CreadoEn);

public record Tema(
    string Id,
    string AnalisisId,
    string Nombre,
    int Orden,
    bool ConfirmadoPorUsuario);

public class Unidad
{
    public long Id { get; set; }
    public required string AnalisisId { get; set; }
    public string? ArchivoId { get; set; }
    public string? TemaId { get; set; }
    public Etapa Etapa { get; set; }
    public EstadoUnidad Estado { get; set; } = EstadoUnidad.Pendiente;
    public string? RutaArtefacto { get; set; }
    public string? HashEntrada { get; set; }
    public string? PromptVersion { get; set; }
    public string? ModeloIa { get; set; }
    public int? Tokens { get; set; }
    public bool FijadoPorUsuario { get; set; }
    public string? ErrorMsg { get; set; }
    public DateTime ActualizadoEn { get; set; }
}
