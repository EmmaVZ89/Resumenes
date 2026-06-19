using Resumenes.Core.Interfaces;
using Resumenes.Infrastructure.Office;
using Xunit;

namespace Resumenes.Tests;

public class ConversorOfficeConFallbackTests
{
    private class ConversorFake(Func<Task<string>> f) : IConversorOffice
    {
        public int Llamadas;
        public Task<string> ConvertirAPdfAsync(string o, string outDir, CancellationToken ct)
        {
            Llamadas++;
            return f();
        }
    }

    [Fact]
    public async Task Usa_primario_si_tiene_exito_y_no_llama_fallback()
    {
        var prim = new ConversorFake(() => Task.FromResult("ok.pdf"));
        var fb = new ConversorFake(() => Task.FromResult("fb.pdf"));
        var c = new ConversorOfficeConFallback(prim, fb);

        var r = await c.ConvertirAPdfAsync("a.docx", "out", default);

        Assert.Equal("ok.pdf", r);
        Assert.Equal(1, prim.Llamadas);
        Assert.Equal(0, fb.Llamadas);
    }

    [Fact]
    public async Task Si_primario_falla_usa_fallback_y_registra()
    {
        var prim = new ConversorFake(() => throw new InvalidOperationException("LO roto"));
        var fb = new ConversorFake(() => Task.FromResult("fb.pdf"));
        var logs = new List<string>();
        var c = new ConversorOfficeConFallback(prim, fb, logs.Add);

        var r = await c.ConvertirAPdfAsync("a.docx", "out", default);

        Assert.Equal("fb.pdf", r);
        Assert.Equal(1, fb.Llamadas);
        Assert.Contains(logs, m => m.Contains("LibreOffice falló"));
    }

    [Fact]
    public async Task Si_ambos_fallan_lanza_con_ambas_causas()
    {
        var prim = new ConversorFake(() => throw new InvalidOperationException("LO roto"));
        var fb = new ConversorFake(() => throw new InvalidOperationException("Office roto"));
        var c = new ConversorOfficeConFallback(prim, fb);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => c.ConvertirAPdfAsync("a.docx", "out", default));

        Assert.Contains("LO roto", ex.Message);
        Assert.Contains("Office roto", ex.Message);
    }

    [Fact]
    public async Task Cancelacion_del_usuario_no_dispara_fallback()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var prim = new ConversorFake(() => throw new OperationCanceledException(cts.Token));
        var fb = new ConversorFake(() => Task.FromResult("fb.pdf"));
        var c = new ConversorOfficeConFallback(prim, fb);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => c.ConvertirAPdfAsync("a.docx", "out", cts.Token));

        Assert.Equal(0, fb.Llamadas);
    }
}
