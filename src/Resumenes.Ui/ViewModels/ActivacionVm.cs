using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Resumenes.Ui.Servicios.Licencia;

namespace Resumenes.Ui.ViewModels;

public partial class ActivacionVm : ObservableObject
{
    private readonly ServicioLicencia _licencia;

    public ActivacionVm(ServicioLicencia licencia)
    {
        _licencia = licencia;
        IdEquipo = licencia.IdEquipo;
    }

    public string IdEquipo { get; }

    [ObservableProperty] private string _clave = "";
    [ObservableProperty] private bool _activando;
    [ObservableProperty] private string? _mensajeError;
    [ObservableProperty] private bool _activada;

    /// <summary>La vista se suscribe para abrir la app principal al activar con éxito.</summary>
    public Action? ActivacionExitosa { get; set; }

    [RelayCommand]
    private async Task ActivarAsync()
    {
        if (string.IsNullOrWhiteSpace(Clave))
        {
            MensajeError = "Ingresá tu clave de activación.";
            return;
        }

        Activando = true;
        MensajeError = null;
        try
        {
            var nombreEquipo = Environment.MachineName;
            var r = await _licencia.ActivarAsync(Clave.Trim(), nombreEquipo, CancellationToken.None);
            if (r.Exitoso)
            {
                Activada = true;
                ActivacionExitosa?.Invoke();
                return;
            }
            MensajeError = MensajePara(r.Error);
        }
        catch
        {
            MensajeError = "No se pudo activar. Probá de nuevo en un momento.";
        }
        finally
        {
            Activando = false;
        }
    }

    private static string MensajePara(string? error) => error switch
    {
        "clave_invalida" => "La clave es inválida. Revisá que la hayas copiado completa.",
        "revocada" => "Esta licencia fue dada de baja. Contactá al proveedor.",
        "limite_alcanzado" => "La clave ya alcanzó su límite de máquinas. Liberá un equipo o pedí otra licencia.",
        "sin_conexion" => "No hay conexión con el servidor. Verificá tu internet e intentá de nuevo.",
        "token_invalido" => "La respuesta del servidor no se pudo verificar. Probá de nuevo.",
        _ => "No se pudo activar. Probá de nuevo en un momento.",
    };
}
