using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;
using OS.Presentation.UI.Services;
using OS.Presentation.UI.Views;

namespace OS.Presentation.UI.Behaviors;

/// <summary>
/// Comportamiento adjunto para modo Demo que bloquea controles y muestra guía interactiva.
/// Al hacer clic en cualquier control con este comportamiento, muestra un diálogo explicativo
/// con la función del módulo en el idioma activo (ES/EN).
/// </summary>
public static class DemoModeBehavior
{
    /// <summary>
    /// Identifica si el control está en modo Demo.
    /// </summary>
    public static readonly DependencyProperty IsDemoModeEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsDemoModeEnabled",
            typeof(bool),
            typeof(DemoModeBehavior),
            new PropertyMetadata(false, OnIsDemoModeEnabledChanged));

    /// <summary>
    /// Identifica el nombre del módulo para mostrar la descripción correspondiente.
    /// </summary>
    public static readonly DependencyProperty ModuleNameProperty =
        DependencyProperty.RegisterAttached(
            "ModuleName",
            typeof(string),
            typeof(DemoModeBehavior),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifica el nombre de la función específica dentro del módulo.
    /// </summary>
    public static readonly DependencyProperty FunctionNameProperty =
        DependencyProperty.RegisterAttached(
            "FunctionName",
            typeof(string),
            typeof(DemoModeBehavior),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// Obtiene si el modo Demo está habilitado para el control.
    /// </summary>
    public static bool GetIsDemoModeEnabled(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsDemoModeEnabledProperty);
    }

    /// <summary>
    /// Establece si el modo Demo está habilitado para el control.
    /// </summary>
    public static void SetIsDemoModeEnabled(DependencyObject obj, bool value)
    {
        obj.SetValue(IsDemoModeEnabledProperty, value);
    }

    /// <summary>
    /// Obtiene el nombre del módulo.
    /// </summary>
    public static string GetModuleName(DependencyObject obj)
    {
        return (string)obj.GetValue(ModuleNameProperty);
    }

    /// <summary>
    /// Establece el nombre del módulo.
    /// </summary>
    public static void SetModuleName(DependencyObject obj, string value)
    {
        obj.SetValue(ModuleNameProperty, value);
    }

    /// <summary>
    /// Obtiene el nombre de la función.
    /// </summary>
    public static string GetFunctionName(DependencyObject obj)
    {
        return (string)obj.GetValue(FunctionNameProperty);
    }

    /// <summary>
    /// Establece el nombre de la función.
    /// </summary>
    public static void SetFunctionName(DependencyObject obj, string value)
    {
        obj.SetValue(FunctionNameProperty, value);
    }

    private static void OnIsDemoModeEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        if ((bool)e.NewValue)
        {
            element.PreviewMouseDown += OnPreviewMouseDown;
            element.IsEnabledChanged += OnIsEnabledChanged;
            UpdateControlState(element);
        }
        else
        {
            element.PreviewMouseDown -= OnPreviewMouseDown;
            element.IsEnabledChanged -= OnIsEnabledChanged;
            RestoreControlState(element);
        }
    }

    private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        var isDemoModeEnabled = GetIsDemoModeEnabled(element);
        if (!isDemoModeEnabled)
            return;

        e.Handled = true;

        var moduleName = GetModuleName(element);
        var functionName = GetFunctionName(element);

        ShowDemoGuideDialog(moduleName, functionName);
    }

    private static void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        var isDemoModeEnabled = GetIsDemoModeEnabled(element);
        if (isDemoModeEnabled)
        {
            UpdateControlState(element);
        }
    }

    private static void UpdateControlState(FrameworkElement element)
    {
        if (!IsInDemoMode())
            return;

        // Guardar el estado original si no se ha guardado
        if (element.ReadLocalValue(OriginalIsEnabledProperty) == DependencyProperty.UnsetValue)
        {
            element.SetValue(OriginalIsEnabledProperty, element.IsEnabled);
        }

        // Deshabilitar el control visualmente pero permitir interacción para mostrar el diálogo
        element.IsEnabled = true;
        
        // Aplicar estilo visual de solo lectura
        if (element is Control control)
        {
            control.Opacity = 0.7;
            control.Cursor = Cursors.Help;
        }
    }

    private static void RestoreControlState(FrameworkElement element)
    {
        // Restaurar el estado original
        if (element.ReadLocalValue(OriginalIsEnabledProperty) != DependencyProperty.UnsetValue)
        {
            var originalEnabled = (bool)element.GetValue(OriginalIsEnabledProperty);
            element.IsEnabled = originalEnabled;
            element.ClearValue(OriginalIsEnabledProperty);
        }

        // Restaurar estilo visual
        if (element is Control control)
        {
            control.Opacity = 1.0;
            control.Cursor = Cursors.Arrow;
        }
    }

    private static bool IsInDemoMode()
    {
        try
        {
            var serviceProvider = App.ServiceProvider;
            if (serviceProvider == null)
                return false;

            var readOnlyModeService = serviceProvider.GetService<IReadOnlyModeService>();
            return readOnlyModeService?.IsReadOnlyMode == true;
        }
        catch
        {
            return false;
        }
    }

    private static void ShowDemoGuideDialog(string moduleName, string functionName)
    {
        try
        {
            var serviceProvider = App.ServiceProvider;
            if (serviceProvider == null)
                return;

            var moduleDescriptionService = serviceProvider.GetService<IModuleDescriptionService>();
            if (moduleDescriptionService == null)
                return;

            var description = !string.IsNullOrEmpty(functionName)
                ? moduleDescriptionService.GetFunctionDescription(moduleName, functionName)
                : moduleDescriptionService.GetModuleDescription(moduleName);

            var dialog = new DemoGuideDialog(description, moduleName, functionName);
            // dialog.Owner = Application.Current.MainWindow; // TODO: Corregir cuando Application.Current esté disponible
            dialog.ShowDialog();
        }
        catch
        {
            // Si falla el diálogo, mostrar un mensaje simple
            MessageBox.Show(
                $"Demo Mode: This feature is read-only.\n\nModule: {moduleName}\nFunction: {functionName}",
                "Demo Mode Guide",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private static readonly DependencyProperty OriginalIsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "OriginalIsEnabled",
            typeof(bool),
            typeof(DemoModeBehavior),
            new PropertyMetadata(false));
}
