using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Resumenes.Licencias.Api.Servicios;

public sealed class FirmadorTokens
{
    private readonly string _pemPrivada;

    public FirmadorTokens(string pemPrivada) => _pemPrivada = pemPrivada;

    public string Firmar(string licenciaId, string hwid, string comprador)
    {
        var ec = ECDsa.Create();
        ec.ImportFromPem(_pemPrivada);
        try
        {
            var key = new ECDsaSecurityKey(ec)
            {
                CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
            };
            var credenciales = new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256);

            var descriptor = new SecurityTokenDescriptor
            {
                Claims = new Dictionary<string, object>
                {
                    ["lic"] = licenciaId,
                    ["hwid"] = hwid,
                    ["sub"] = comprador,
                },
                IssuedAt = DateTime.UtcNow,
                SigningCredentials = credenciales,
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }
        finally
        {
            ec.Dispose();
        }
    }
}
