namespace Resumenes.Core.Interfaces;

/// <summary>Fase actual de la descarga de un bundle.</summary>
public enum FaseDescarga { LeyendoManifest, Descargando, Verificando, Descomprimiendo, Completado, Error }

/// <summary>Estado de progreso reportado durante la descarga.</summary>
public record EstadoDescarga(
    string BundleId, FaseDescarga Fase, long BytesActual, long BytesTotal,
    int BundleIndice, int BundleTotal, string Detalle);

/// <summary>Resultado agregado de descargar todos los bundles faltantes.</summary>
public record ResultadoDescarga(int Ok, int Salteados, int Errores, IReadOnlyList<string> Fallos);

/// <summary>Descarga, verifica y descomprime las dependencias externas según un manifest.</summary>
public interface IDescargadorDependencias
{
    Task<ResultadoDescarga> DescargarFaltantesAsync(IProgress<EstadoDescarga>? progreso, CancellationToken ct);
}
