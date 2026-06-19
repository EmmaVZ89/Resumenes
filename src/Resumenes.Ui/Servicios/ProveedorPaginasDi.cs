using Wpf.Ui.Abstractions;

namespace Resumenes.Ui.Servicios;

/// <summary>
/// Implementa <see cref="INavigationViewPageProvider"/> usando el contenedor DI de la aplicación,
/// para que el <c>NavigationView</c> de WPF-UI resuelva las páginas con sus dependencias (ViewModels, etc.)
/// en lugar de invocar el constructor sin parámetros.
/// </summary>
public sealed class ProveedorPaginasDi : INavigationViewPageProvider
{
    private readonly IServiceProvider _sp;

    public ProveedorPaginasDi(IServiceProvider sp) => _sp = sp;

    /// <inheritdoc />
    public object? GetPage(Type pageType) => _sp.GetService(pageType);
}
