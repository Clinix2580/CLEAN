using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;

namespace OS.Presentation.UI.Attached;

/// <summary>
/// Propiedad adjunta para aplicar estado visual de solo lectura a controles.
/// Desactiva el control y aplica un efecto visual cuando está en modo Demo ReadOnly.
/// </summary>
public static class ReadOnlyModeProperty
{
    public static bool GetApplyReadOnlyMode(DependencyObject obj) =>
        (bool)obj.GetValue(ApplyReadOnlyModeProperty);

    public static void SetApplyReadOnlyMode(DependencyObject obj, bool value) =>
        obj.SetValue(ApplyReadOnlyModeProperty, value);

    public static readonly DependencyProperty ApplyReadOnlyModeProperty =
        DependencyProperty.RegisterAttached(
            "ApplyReadOnlyMode",
            typeof(bool),
            typeof(ReadOnlyModeProperty),
            new PropertyMetadata(false, OnApplyReadOnlyModeChanged));

    private static void OnApplyReadOnlyModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue)
            return;

        if (d is not Control control)
            return;

        try
        {
            var readOnlyModeService = App.ServiceProvider.GetRequiredService<IReadOnlyModeService>();
            
            if (readOnlyModeService.IsReadOnlyMode)
            {
                control.IsEnabled = false;
                control.Opacity = 0.6;
                control.Background = new SolidColorBrush(Colors.LightGray) { Opacity = 0.3 };
            }
            else
            {
                control.IsEnabled = true;
                control.Opacity = 1.0;
                control.Background = null;
            }
        }
        catch
        {
            // Si el servicio no está disponible, no aplicar estilos
        }
    }
}
