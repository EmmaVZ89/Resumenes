namespace Resumenes.Licencias.Api.Datos;

public class Licencia
{
    public Guid Id { get; set; }
    public string Clave { get; set; } = "";
    public string Comprador { get; set; } = "";
    public string Email { get; set; } = "";
    public int MaxMaquinas { get; set; } = 2;
    public string Estado { get; set; } = "activa";
    public DateTimeOffset CreadaEn { get; set; }
    public string? Notas { get; set; }
    public List<Activacion> Activaciones { get; set; } = new();
}
