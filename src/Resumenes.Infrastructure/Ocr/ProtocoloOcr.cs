using System.Text.Json;

namespace Resumenes.Infrastructure.Ocr;

public record MensajeOcr(string Tipo, string? ReqId, int? Pagina, string? Texto, string? Mensaje);

public static class ProtocoloOcr
{
    public static MensajeOcr? Parsear(string lineaNdjson)
    {
        if (string.IsNullOrWhiteSpace(lineaNdjson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(lineaNdjson);
            var e = doc.RootElement;
            if (!e.TryGetProperty("type", out var tipoEl)) return null;
            return new MensajeOcr(
                tipoEl.GetString() ?? "",
                e.TryGetProperty("req_id", out var r) ? r.GetString() : null,
                e.TryGetProperty("pagina", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null,
                e.TryGetProperty("texto", out var t) ? t.GetString() : null,
                e.TryGetProperty("mensaje", out var m) ? m.GetString() : null);
        }
        catch (JsonException) { return null; }
    }
}
