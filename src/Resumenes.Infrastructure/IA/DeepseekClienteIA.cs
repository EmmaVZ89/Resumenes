using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Resumenes.Core.Interfaces;

namespace Resumenes.Infrastructure.IA;

public class DeepseekClienteIA(HttpClient http, IAlmacenSecretos secretos, string baseUrl) : IClienteIA
{
    public async Task<RespuestaIA> CompletarAsync(SolicitudIA req, CancellationToken ct)
    {
        var key = secretos.ObtenerApiKey()
            ?? throw new InvalidOperationException("No hay API key de Deepseek configurada.");

        var cuerpo = new
        {
            model = req.Modelo,
            temperature = req.Temperatura,
            max_tokens = req.MaxTokens,
            messages = new object[]
            {
                new { role = "system", content = req.PromptSystem },
                new { role = "user", content = req.PromptUser }
            }
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = JsonContent.Create(cuerpo)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        using var resp = await http.SendAsync(msg, ct);
        var contenido = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Deepseek devolvió {(int)resp.StatusCode} ({resp.StatusCode}) para el modelo '{req.Modelo}'. " +
                $"Respuesta del API: {Recortar(contenido)}");

        using var doc = JsonDocument.Parse(contenido);
        var root = doc.RootElement;
        var choice = root.GetProperty("choices")[0];
        var texto = choice.GetProperty("message").GetProperty("content").GetString() ?? "";
        var finish = choice.GetProperty("finish_reason").GetString() ?? "";
        var usage = root.GetProperty("usage");

        return new RespuestaIA(
            texto, finish,
            usage.GetProperty("prompt_tokens").GetInt32(),
            usage.GetProperty("completion_tokens").GetInt32(),
            usage.GetProperty("total_tokens").GetInt32());
    }

    private static string Recortar(string s, int max = 600)
        => string.IsNullOrWhiteSpace(s) ? "(vacía)" : (s.Length <= max ? s : s[..max] + "…");
}
