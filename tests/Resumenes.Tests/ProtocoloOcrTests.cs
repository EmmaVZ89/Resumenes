using Resumenes.Infrastructure.Ocr;
using Xunit;

namespace Resumenes.Tests;

public class ProtocoloOcrTests
{
    [Fact]
    public void Parsea_result()
    {
        var m = ProtocoloOcr.Parsear("""{"type":"result","req_id":"r1","pagina":2,"texto":"hola ñ"}""");
        Assert.NotNull(m);
        Assert.Equal("result", m!.Tipo);
        Assert.Equal("r1", m.ReqId);
        Assert.Equal(2, m.Pagina);
        Assert.Equal("hola ñ", m.Texto);
    }

    [Fact]
    public void Parsea_ready_yError()
    {
        Assert.Equal("ready", ProtocoloOcr.Parsear("""{"type":"ready"}""")!.Tipo);
        var e = ProtocoloOcr.Parsear("""{"type":"error","req_id":"r1","mensaje":"boom"}""");
        Assert.Equal("error", e!.Tipo);
        Assert.Equal("boom", e.Mensaje);
    }

    [Fact]
    public void LineaInvalida_devuelveNull()
    {
        Assert.Null(ProtocoloOcr.Parsear("no es json"));
        Assert.Null(ProtocoloOcr.Parsear(""));
    }
}
