using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Resumenes.Core.Licencias;

public sealed class ValidadorTokenLicencia
{
    // Clave pública EC P-256 (SPKI base64) que corresponde a la privada del servidor.
    private const string ClavePublicaSpki =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAElCSdQgdpkcB1ac/4WXtZZpFcOMy4N8MUClrZoB/7Shg8qELTAggKLOUm16OQzJFVjb1L9GnLNv5MH/kX2UxbUg==";

    private readonly byte[] _pubDer;

    public ValidadorTokenLicencia() : this(ClavePublicaSpki) { }

    internal ValidadorTokenLicencia(string pubSpkiBase64)
        => _pubDer = Convert.FromBase64String(pubSpkiBase64);

    public ResultadoValidacionToken Validar(string token, string hwidEsperado)
    {
        if (string.IsNullOrWhiteSpace(token)) return ResultadoValidacionToken.Invalido;
        var partes = token.Split('.');
        if (partes.Length != 3) return ResultadoValidacionToken.Invalido;

        try
        {
            var firma = DesdeB64Url(partes[2]);
            using var ec = ECDsa.Create();
            ec.ImportSubjectPublicKeyInfo(_pubDer, out _);
            var ok = ec.VerifyData(
                Encoding.ASCII.GetBytes($"{partes[0]}.{partes[1]}"),
                firma, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            if (!ok) return ResultadoValidacionToken.Invalido;

            using var doc = JsonDocument.Parse(DesdeB64Url(partes[1]));
            var raiz = doc.RootElement;
            var lic = raiz.GetProperty("lic").GetString() ?? "";
            var hwid = raiz.GetProperty("hwid").GetString() ?? "";
            var sub = raiz.TryGetProperty("sub", out var s) ? s.GetString() ?? "" : "";
            var iat = raiz.TryGetProperty("iat", out var i) ? i.GetInt64() : 0;

            if (!string.Equals(hwid, hwidEsperado, StringComparison.Ordinal))
                return ResultadoValidacionToken.Invalido;

            return new ResultadoValidacionToken(true,
                new ClaimsLicencia(lic, hwid, sub, DateTimeOffset.FromUnixTimeSeconds(iat).UtcDateTime));
        }
        catch
        {
            return ResultadoValidacionToken.Invalido;
        }
    }

    private static byte[] DesdeB64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        s += (s.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(s);
    }
}
