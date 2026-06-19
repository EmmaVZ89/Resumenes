using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Resumenes.Ui.Controles;

/// <summary>
/// Control de animación "pensante": 3 puntos que pulsan en ola infinita.
/// Propiedades de dependencia: Estado (string) y Activo (bool).
/// La animación es pura XAML (Storyboard), no bloquea el hilo de UI.
/// </summary>
public partial class LoaderPensante : UserControl
{
    // DP: Estado — texto descriptivo enlazable desde el exterior
    public static readonly DependencyProperty EstadoProperty =
        DependencyProperty.Register(
            nameof(Estado),
            typeof(string),
            typeof(LoaderPensante),
            new PropertyMetadata(string.Empty));

    public string Estado
    {
        get => (string)GetValue(EstadoProperty);
        set => SetValue(EstadoProperty, value);
    }

    // DP: Activo — arranca o detiene la animación
    public static readonly DependencyProperty ActivoProperty =
        DependencyProperty.Register(
            nameof(Activo),
            typeof(bool),
            typeof(LoaderPensante),
            new PropertyMetadata(true, OnActivoChanged));

    public bool Activo
    {
        get => (bool)GetValue(ActivoProperty);
        set => SetValue(ActivoProperty, value);
    }

    private Storyboard? _sb1, _sb2, _sb3;

    public LoaderPensante()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _sb1 = (Storyboard)Resources["AnimPunto1"];
        _sb2 = (Storyboard)Resources["AnimPunto2"];
        _sb3 = (Storyboard)Resources["AnimPunto3"];

        if (Activo)
            IniciarAnimacion();
    }

    private static void OnActivoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LoaderPensante loader) return;
        if ((bool)e.NewValue)
            loader.IniciarAnimacion();
        else
            loader.DetenerAnimacion();
    }

    private void IniciarAnimacion()
    {
        _sb1?.Begin(this, true);
        _sb2?.Begin(this, true);
        _sb3?.Begin(this, true);
    }

    private void DetenerAnimacion()
    {
        _sb1?.Stop(this);
        _sb2?.Stop(this);
        _sb3?.Stop(this);
    }
}
