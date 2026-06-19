namespace Resumenes.Core.Interfaces;

public record SolicitudIA(
    string PromptSystem,
    string PromptUser,
    double Temperatura,
    int MaxTokens,
    string PromptVersion,
    string Modelo);

public record RespuestaIA(
    string Texto,
    string FinishReason,
    int TokensPrompt,
    int TokensCompletion,
    int TokensTotal);

public interface IClienteIA
{
    Task<RespuestaIA> CompletarAsync(SolicitudIA req, CancellationToken ct);
}
