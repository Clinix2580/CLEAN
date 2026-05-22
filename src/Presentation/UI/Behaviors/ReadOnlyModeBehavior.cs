using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors;
using OS.Application.Interfaces;
using OS.Presentation.UI.Services;

namespace OS.Presentation.UI.Behaviors;

/// <summary>
/// Comportamiento MVVM que bloquea interacción en controles cuando está activo el modo Solo Lectura.
/// Muestra un diálogo explicativo con descripción de la función del módulo al intentar editar.
/// </summary>
public class ReadOnlyModeBehavior : Behavior<Control>
{
    private IReadOnlyModeService? _readOnlyModeService;
    private IModuleDescriptionService? _moduleDescriptionService;

    /// <summary>
    /// Propiedad de dependencia para especificar el nombre del módulo asociado al control.
    /// Se usa para mostrar descripciones específicas en el diálogo.
    /// </summary>
    public static readonly DependencyProperty ModuleNameProperty =
        DependencyProperty.RegisterAttached(
            "ModuleName",
            typeof(string),
            typeof(ReadOnlyModeBehavior),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// Propiedad de dependencia para especificar el nombre de la función asociada al control.
    /// Se usa para mostrar descripciones específicas en el diálogo.
    /// </summary>
    public static readonly DependencyProperty FunctionNameProperty =
        DependencyProperty.RegisterAttached(
            "FunctionName",
            typeof(string),
            typeof(ReadOnlyModeBehavior),
            new PropertyMetadata(string.Empty));

    public static string GetModuleName(DependencyObject obj) =>
        (string)obj.GetValue(ModuleNameProperty);

    public static void SetModuleName(DependencyObject obj, string value) =>
        obj.SetValue(ModuleNameProperty, value);

    public static string GetFunctionName(DependencyObject obj) =>
        (string)obj.GetValue(FunctionNameProperty);

    public static void SetFunctionName(DependencyObject obj, string value) =>
        obj.SetValue(FunctionNameProperty, value);

    protected override void OnAttached()
    {
        base.OnAttached();
        
        if (AssociatedObject != null)
        {
            AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseDown;
            AssociatedObject.KeyDown += OnKeyDown;
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseDown;
            AssociatedObject.KeyDown -= OnKeyDown;
        }
        
        base.OnDetaching();
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _readOnlyModeService = App.ServiceProvider.GetRequiredService<IReadOnlyModeService>();
        _moduleDescriptionService = App.ServiceProvider.GetRequiredService<IModuleDescriptionService>();
        
        if (_readOnlyModeService?.IsReadOnlyMode == true)
        {
            e.Handled = true;
            ShowReadOnlyDialog();
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        _readOnlyModeService = App.ServiceProvider.GetRequiredService<IReadOnlyModeService>();
        _moduleDescriptionService = App.ServiceProvider.GetRequiredService<IModuleDescriptionService>();
        
        if (_readOnlyModeService?.IsReadOnlyMode == true && IsEditingKey(e.Key))
        {
            e.Handled = true;
            ShowReadOnlyDialog();
        }
    }

    private static bool IsEditingKey(Key key) =>
        key == Key.Delete || key == Key.Back || 
        (key >= Key.A && key <= Key.Z) || 
        (key >= Key.D0 && key <= Key.D9);

    private void ShowReadOnlyDialog()
    {
        var moduleName = GetModuleName(AssociatedObject);
        var functionName = GetFunctionName(AssociatedObject);
        
        string moduleDescription = string.Empty;
        string functionDescription = string.Empty;

        if (!string.IsNullOrWhiteSpace(moduleName) && _moduleDescriptionService != null)
        {
            moduleDescription = _moduleDescriptionService.GetModuleDescription(moduleName);
            
            if (!string.IsNullOrWhiteSpace(functionName))
            {
                functionDescription = _moduleDescriptionService.GetFunctionDescription(moduleName, functionName);
            }
        }

        var message = _readOnlyModeService?.Reason ?? "Modo Solo Lectura activo.";
        
        #if LANG_ES
            var title = "Modo Demo - Solo Lectura";
            var fullMessage = BuildSpanishMessage(message, moduleName, moduleDescription, functionName, functionDescription);
        #else
            var title = "Demo Mode - Read Only";
            var fullMessage = BuildEnglishMessage(message, moduleName, moduleDescription, functionName, functionDescription);
        #endif

        MessageBox.Show(fullMessage, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string BuildSpanishMessage(
        string reason, 
        string moduleName, 
        string moduleDescription,
        string functionName,
        string functionDescription)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(reason);
        sb.AppendLine();
        
        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            sb.AppendLine($"Módulo: {moduleName}");
            if (!string.IsNullOrWhiteSpace(moduleDescription))
            {
                sb.AppendLine($"Descripción: {moduleDescription}");
            }
            
            if (!string.IsNullOrWhiteSpace(functionName))
            {
                sb.AppendLine($"Función: {functionName}");
                if (!string.IsNullOrWhiteSpace(functionDescription))
                {
                    sb.AppendLine($"Descripción: {functionDescription}");
                }
            }
            sb.AppendLine();
        }
        
        sb.AppendLine("Esta es una versión demo con acceso de solo lectura.");
        sb.AppendLine("Para obtener acceso de escritura y todas las funcionalidades,");
        sb.AppendLine("por favor contacte a soporte técnico.");
        
        return sb.ToString();
    }

    private static string BuildEnglishMessage(
        string reason, 
        string moduleName, 
        string moduleDescription,
        string functionName,
        string functionDescription)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(reason);
        sb.AppendLine();
        
        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            sb.AppendLine($"Module: {moduleName}");
            if (!string.IsNullOrWhiteSpace(moduleDescription))
            {
                sb.AppendLine($"Description: {moduleDescription}");
            }
            
            if (!string.IsNullOrWhiteSpace(functionName))
            {
                sb.AppendLine($"Function: {functionName}");
                if (!string.IsNullOrWhiteSpace(functionDescription))
                {
                    sb.AppendLine($"Description: {functionDescription}");
                }
            }
            sb.AppendLine();
        }
        
        sb.AppendLine("This is a demo version with read-only access.");
        sb.AppendLine("To obtain write access and all functionalities,");
        sb.AppendLine("please contact technical support.");
        
        return sb.ToString();
    }
}
