namespace Resumenes.Core.Interfaces;

public interface IAlmacenSecretos
{
    void GuardarApiKey(string key);
    string? ObtenerApiKey();
}
