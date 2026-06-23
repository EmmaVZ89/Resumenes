namespace Resumenes.Licencias.Api.Datos;

public class Activacion
{
    public Guid Id { get; set; }
    public Guid LicenciaId { get; set; }
    public Licencia Licencia { get; set; } = null!;
    public string Hwid { get; set; } = "";
    public string NombreEquipo { get; set; } = "";
    public DateTimeOffset PrimeraActivacion { get; set; }
    public DateTimeOffset UltimaValidacion { get; set; }
}
