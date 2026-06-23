using System.Net.Http;
using System.Text.Json;

namespace Resumenes.Ui.Servicios;

/// <summary>
/// Chequea (una vez por sesión) si hay una versión más nueva publicada en GitHub Releases.
/// Best-effort: cualquier error (sin internet, API caída, rate limit) se traga y devuelve null,
/// de modo que el aviso nunca interrumpe ni rompe el arranque de la app.
/// </summary>
public sealed class ServicioActualizaciones
{
    private readonly HttpClient _http;
    private readonly string _urlApi;
    private readonly Version _versionActual;
    private bool _yaChequeo;
    private InfoActualizacion? _cache;

    public ServicioActualizaciones(HttpClient http, string urlApi, Version versionActual)
    {
        _http = http;
        _urlApi = urlApi;
        _versionActual = versionActual;
    }

    /// <summary>
    /// Devuelve la info de actualización si hay una versión publicada más nueva, o null.
    /// Memoiza: solo consulta GitHub la primera vez por sesión.
    /// </summary>
    public async Task<InfoActualizacion?> ChequearAsync(CancellationToken ct = default)
    {
        if (_yaChequeo) return _cache;
        _yaChequeo = true;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, _urlApi);
            // GitHub exige User-Agent; Accept fija el formato de la API REST v3.
            req.Headers.UserAgent.ParseAdd("ResumenesApp-UpdateCheck");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync(ct);
            _cache = Evaluar(json, _versionActual);
            return _cache;
        }
        catch
        {
            return null; // sin internet / API caída ⇒ sin aviso
        }
    }

    /// <summary>
    /// Lógica pura testeable: dado el JSON de <c>releases/latest</c> y la versión actual,
    /// devuelve la info si la publicada es más nueva, o null en cualquier otro caso.
    /// </summary>
    public static InfoActualizacion? Evaluar(string json, Version versionActual)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var url = root.TryGetProperty("html_url", out var u) ? u.GetString() : null;
            var nueva = ParsearVersion(tag);
            if (nueva is null || string.IsNullOrWhiteSpace(url)) return null;
            return EsMasNueva(nueva, versionActual)
                ? new InfoActualizacion(Etiqueta(nueva), url!)
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Convierte "v1.2.0" / "1.2" / "v2.0.0-beta" en Version (toma solo Major.Minor.Build), o null.</summary>
    public static Version? ParsearVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var s = tag.Trim().TrimStart('v', 'V');
        int n = 0;
        while (n < s.Length && (char.IsDigit(s[n]) || s[n] == '.')) n++;
        s = s[..n].TrimEnd('.');
        return Version.TryParse(s, out var v) ? v : null;
    }

    /// <summary>Compara Major.Minor.Build (ignora la revisión y los -1 de Version.Parse).</summary>
    public static bool EsMasNueva(Version nueva, Version actual) => Norm(nueva) > Norm(actual);

    private static Version Norm(Version v) => new(v.Major, Max0(v.Minor), Max0(v.Build));
    private static int Max0(int x) => x < 0 ? 0 : x;
    private static string Etiqueta(Version v) => $"{v.Major}.{Max0(v.Minor)}.{Max0(v.Build)}";
}

/// <summary>Versión publicada más nueva y la URL de la release para descargarla.</summary>
public record InfoActualizacion(string Version, string Url);
