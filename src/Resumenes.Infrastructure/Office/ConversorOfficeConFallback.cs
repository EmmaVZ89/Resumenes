using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.Office;

/// <summary>
/// Conversor Office→PDF con red de seguridad: intenta primero el conversor primario
/// (LibreOffice portable) y, si falla o se cuelga (timeout), recurre al fallback
/// (Microsoft Office vía COM, si está instalado). Si ambos fallan, lanza un error
/// que combina ambas causas. El <paramref name="log"/> opcional registra por qué falló
/// el primario aunque el fallback tenga éxito (útil para diagnosticar LibreOffice).
/// </summary>
public class ConversorOfficeConFallback(
    IConversorOffice primario,
    IConversorOffice fallback,
    Action<string>? log = null) : IConversorOffice
{
    public async Task<string> ConvertirAPdfAsync(string archivoOffice, string outDir, CancellationToken ct)
    {
        try
        {
            return await primario.ConvertirAPdfAsync(archivoOffice, outDir, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // cancelación real del usuario: no intentar el fallback
        }
        catch (Exception exPrimario)
        {
            var nombre = Path.GetFileName(archivoOffice);
            log?.Invoke($"[{nombre}] LibreOffice falló: {exPrimario.Message}. Intentando con Microsoft Office…");
            try
            {
                var pdf = await fallback.ConvertirAPdfAsync(archivoOffice, outDir, ct);
                log?.Invoke($"[{nombre}] Convertido con Microsoft Office (fallback).");
                return pdf;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exFallback)
            {
                log?.Invoke($"[{nombre}] Fallback con Office también falló: {exFallback.Message}");
                throw new InvalidOperationException(
                    $"No se pudo convertir '{nombre}' a PDF. " +
                    $"LibreOffice: {exPrimario.Message} || Office: {exFallback.Message}", exFallback);
            }
        }
    }
}
