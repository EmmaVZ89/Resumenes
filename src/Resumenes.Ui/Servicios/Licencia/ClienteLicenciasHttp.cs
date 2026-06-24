using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Resumenes.Core.Licencias;

namespace Resumenes.Ui.Servicios.Licencia;

public sealed class ClienteLicenciasHttp : IClienteLicencias
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public ClienteLicenciasHttp(HttpClient http, string baseUrl)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<ResultadoActivacion> ActivarAsync(string clave, string hwid, string nombreEquipo, CancellationToken ct)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}/activar",
                new { clave, hwid, nombreEquipo }, ct);
            var cuerpo = await resp.Content.ReadAsStringAsync(ct);
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var token = LeerCampo(cuerpo, "token");
                return new ResultadoActivacion(true, token, null);
            }
            return new ResultadoActivacion(false, null, LeerCampo(cuerpo, "error") ?? "error");
        }
        catch
        {
            return new ResultadoActivacion(false, null, "sin_conexion");
        }
    }

    public async Task<EstadoValidacionServidor> ValidarAsync(string licenciaId, string hwid, CancellationToken ct)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"{_baseUrl}/validar",
                new { licenciaId, hwid }, ct);
            return resp.StatusCode == HttpStatusCode.OK
                ? EstadoValidacionServidor.Activa
                : EstadoValidacionServidor.Revocada;
        }
        catch
        {
            return EstadoValidacionServidor.SinConexion;
        }
    }

    private static string? LeerCampo(string json, string campo)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(campo, out var v) ? v.GetString() : null;
        }
        catch { return null; }
    }
}
