using Resumenes.Core.Interfaces;
using Resumenes.Core.Modelos;
using Resumenes.Core.Orquestacion;
using Resumenes.Infrastructure.Aplicacion;
using Resumenes.Tests.Fakes;
using Xunit;

namespace Resumenes.Tests;

public class ServicioAnalisisTests
{
    [Fact]
    public async Task ProcesarArchivos_emite_progreso_y_marca_ok()
    {
        // Carpeta temporal con 1 txt (camino TXT: sin OCR/Office).
        var dir = Path.Combine(Path.GetTempPath(), "sa_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.txt"), "contenido de prueba");
        var ws = Path.Combine(dir, "ws"); Directory.CreateDirectory(ws);
        try
        {
            var repo = new RepositorioEnMemoria();
            var servicio = ServicioAnalisisFactory.ParaTests(repo, ws); // arma el servicio con fakes
            var an = await servicio.AbrirOCrearAsync(dir, default);
            var eventos = new List<ProgresoPaso>();
            var r = await servicio.ProcesarArchivosAsync(an, new Progress<ProgresoPaso>(eventos.Add), default);
            await Task.Delay(50);

            Assert.Equal(1, r.Ok);
            Assert.Equal(0, r.Error);
            Assert.Contains(eventos, e => e.Fase == FaseAnalisis.Limpieza);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
