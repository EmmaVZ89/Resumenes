using Resumenes.Core.Interfaces;

namespace Resumenes.Core.Apoyos;

public class RelojUtcSistema : IRelojUtc
{
    public DateTime Ahora() => DateTime.UtcNow;
}
