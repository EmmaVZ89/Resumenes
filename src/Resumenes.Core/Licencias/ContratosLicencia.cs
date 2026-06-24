namespace Resumenes.Core.Licencias;

public enum EstadoLicenciaCliente { SinLicencia, Activa, RevalidarAhora, BloqueadaPorGracia, Revocada }

public record ClaimsLicencia(string LicenciaId, string Hwid, string Comprador, DateTime EmitidoEn);

public record ResultadoValidacionToken(bool Valido, ClaimsLicencia? Claims)
{
    public static ResultadoValidacionToken Invalido { get; } = new(false, null);
}

public record DatosLicenciaGuardada(string Token, DateTime UltimaValidacionExitosa);

public enum EstadoValidacionServidor { Activa, Revocada, SinConexion }

public record ResultadoActivacion(bool Exitoso, string? Token, string? Error);
