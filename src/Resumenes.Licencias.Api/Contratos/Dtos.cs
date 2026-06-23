namespace Resumenes.Licencias.Api.Contratos;

public enum CodigoActivacion { Ok, ClaveInvalida, Revocada, LimiteAlcanzado }
public record ResultadoActivacion(CodigoActivacion Codigo, string? Token);

public enum EstadoValidacion { Activa, Revocada }

public record ActivarRequest(string Clave, string Hwid, string NombreEquipo);
public record ActivarResponse(string Token);
public record ValidarRequest(string LicenciaId, string Hwid);
public record ValidarResponse(string Estado);

public record CrearLicenciaRequest(string Comprador, string Email, int? MaxMaquinas);
public record CrearLicenciaResponse(Guid Id, string Clave, string Comprador, int MaxMaquinas);
