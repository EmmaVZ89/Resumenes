namespace Resumenes.Licencias.Api.Datos;

public static class ConfiguracionBd
{
    public static bool EsPostgres(string? databaseUrl)
        => !string.IsNullOrWhiteSpace(databaseUrl);

    public static string ConnectionStringDesde(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var partes = uri.UserInfo.Split(':', 2);
        var usuario = Uri.UnescapeDataString(partes[0]);
        var clave = partes.Length > 1 ? Uri.UnescapeDataString(partes[1]) : "";
        var baseDatos = uri.AbsolutePath.TrimStart('/');
        var puerto = uri.Port > 0 ? uri.Port : 5432;

        return $"Host={uri.Host};Port={puerto};Username={usuario};" +
               $"Password={clave};Database={baseDatos};SSL Mode=Require;Trust Server Certificate=true";
    }
}
