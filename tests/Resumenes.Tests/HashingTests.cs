using Resumenes.Core.Apoyos;
using Xunit;

namespace Resumenes.Tests;

public class HashingTests
{
    [Fact]
    public void Sha256DeTexto_esDeterministaYConocido()
    {
        // SHA-256 de "abc" (UTF-8) es un valor conocido.
        var hash = Hashing.Sha256HexDeTexto("abc");
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    [Fact]
    public void ArchivoId_tomaLos16PrimerosHex()
    {
        var hash = Hashing.Sha256HexDeTexto("abc");
        Assert.Equal("ba7816bf8f01cfea", Hashing.ArchivoIdDesdeHash(hash));
        Assert.Equal(16, Hashing.ArchivoIdDesdeHash(hash).Length);
    }
}
