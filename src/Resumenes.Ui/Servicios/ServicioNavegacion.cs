using Wpf.Ui.Controls;

namespace Resumenes.Ui.Servicios;

/// <summary>
/// Envoltorio del NavigationView de WPF-UI para navegar entre páginas por tipo
/// y pasar parámetros opcionales (p. ej. el Analisis en curso).
///
/// El parámetro NO se pasa como DataContext (eso provocaba que WPF-UI pisara el
/// DataContext del VM con el parámetro y dejara los bindings rotos). En su lugar se
/// guarda aquí y la página lo consume en su evento Loaded vía <see cref="ConsumirParametro"/>,
/// manteniendo su ViewModel como DataContext desde el primer instante.
/// </summary>
public class ServicioNavegacion
{
    private NavigationView? _nav;
    private object? _parametroPendiente;

    /// <summary>
    /// Asocia este servicio al control NavigationView de la ventana principal
    /// y configura el proveedor de páginas respaldado por el contenedor DI.
    /// Debe llamarse desde el constructor de MainWindow (después de InitializeComponent).
    /// </summary>
    public void AsociarNavigationView(NavigationView nav)
    {
        _nav = nav;
        _nav.SetPageProviderService(new ProveedorPaginasDi(App.Servicios));
    }

    /// <summary>
    /// Devuelve (y limpia) el parámetro de la navegación en curso. La página destino
    /// lo llama una sola vez en su Loaded. Devuelve null si no había parámetro.
    /// </summary>
    public object? ConsumirParametro()
    {
        var p = _parametroPendiente;
        _parametroPendiente = null;
        return p;
    }

    /// <summary>Navega a la vista del tipo <typeparamref name="TVista"/> con un parámetro opcional.</summary>
    public void Navegar<TVista>(object? parametro = null) where TVista : class
        => Navegar(typeof(TVista), parametro);

    /// <summary>Navega a la vista del tipo indicado (sobrecarga no-genérica) con un parámetro opcional.</summary>
    public void Navegar(Type tipoVista, object? parametro = null)
    {
        if (_nav is null)
            throw new InvalidOperationException("ServicioNavegacion no está asociado a un NavigationView.");

        _parametroPendiente = parametro;
        _nav.Navigate(tipoVista);
    }
}
