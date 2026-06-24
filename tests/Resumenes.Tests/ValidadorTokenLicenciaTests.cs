using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Resumenes.Core.Licencias;

namespace Resumenes.Tests;

public class ValidadorTokenLicenciaTests
{
    // Firma un JWT ES256 de prueba con la privada dada (formato P1363, como el servidor).
    private static string FirmarJwt(ECDsa ec, string lic, string hwid, string sub, long iat)
    {
        static string B64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64Url(Encoding.UTF8.GetBytes("{\"alg\":\"ES256\",\"typ\":\"JWT\"}"));
        var payloadJson = JsonSerializer.Serialize(new { lic, hwid, sub, iat });
        var payload = B64Url(Encoding.UTF8.GetBytes(payloadJson));
        var firma = ec.SignData(Encoding.ASCII.GetBytes($"{header}.{payload}"),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{header}.{payload}.{B64Url(firma)}";
    }

    [Fact]
    public void Validar_TokenBienFirmadoYHwidCoincide_EsValido()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pub = Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo());
        var token = FirmarJwt(ec, "lic-1", "hw-abc", "Juan", 1700000000);

        var sut = new ValidadorTokenLicencia(pub);
        var r = sut.Validar(token, "hw-abc");

        Assert.True(r.Valido);
        Assert.Equal("lic-1", r.Claims!.LicenciaId);
        Assert.Equal("hw-abc", r.Claims.Hwid);
        Assert.Equal("Juan", r.Claims.Comprador);
    }

    [Fact]
    public void Validar_HwidDistinto_NoEsValido()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pub = Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo());
        var token = FirmarJwt(ec, "lic-1", "hw-abc", "Juan", 1700000000);

        var r = new ValidadorTokenLicencia(pub).Validar(token, "hw-OTRO");

        Assert.False(r.Valido);
    }

    [Fact]
    public void Validar_FirmadoConOtraClave_NoEsValido()
    {
        using var ecFirma = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var ecOtra = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pubOtra = Convert.ToBase64String(ecOtra.ExportSubjectPublicKeyInfo());
        var token = FirmarJwt(ecFirma, "lic-1", "hw-abc", "Juan", 1700000000);

        var r = new ValidadorTokenLicencia(pubOtra).Validar(token, "hw-abc");

        Assert.False(r.Valido);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-jwt")]
    [InlineData("a.b")]
    [InlineData("a.b.c")]
    [InlineData("a.b.c.d")]
    public void Validar_TokenMalFormado_NoEsValido(string token)
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pub = Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo());
        Assert.False(new ValidadorTokenLicencia(pub).Validar(token, "hw-abc").Valido);
    }
}
