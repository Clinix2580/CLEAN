using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OS.Application.Interfaces;

namespace OS.Presentation.UI.Services;

public static class ReadOnlyUiGuard
{
    private static readonly string[] WriteActionTokens =
    [
        "save",
        "guardar",
        "edit",
        "editar",
        "delete",
        "eliminar",
        "new",
        "nuevo",
        "add",
        "agregar",
        "create",
        "crear",
        "approve",
        "aprobar",
        "deny",
        "denegar",
        "activate",
        "activar",
        "charge",
        "cobrar",
        "void",
        "cancelar",
        "dispense",
        "surtir"
    ];

    private static readonly string[] ReadActionTokens =
    [
        "search",
        "buscar",
        "filter",
        "filtrar",
        "refresh",
        "actualizar",
        "view",
        "ver",
        "consult",
        "consultar",
        "open",
        "abrir",
        "logout"
    ];

    public static void Apply(DependencyObject root, IReadOnlyModeService readOnlyModeService)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(readOnlyModeService);

        if (!readOnlyModeService.IsReadOnlyMode)
            return;

        ApplyRecursive(root, readOnlyModeService.Reason);
    }

    private static void ApplyRecursive(DependencyObject root, string reason)
    {
        if (root is Control control && ShouldDisable(control))
        {
            control.IsEnabled = false;
            control.ToolTip = string.IsNullOrWhiteSpace(reason)
                ? "Modo Solo Lectura activo por expiracion o restriccion de licencia."
                : reason;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
            ApplyRecursive(VisualTreeHelper.GetChild(root, index), reason);
    }

    private static bool ShouldDisable(Control control)
    {
        if (control is TextBox or PasswordBox or ComboBox or DatePicker or CheckBox or RadioButton or Slider)
            return true;

        var typeName = control.GetType().Name;
        if (!typeName.Contains("Button", StringComparison.OrdinalIgnoreCase))
            return false;

        var descriptor = $"{control.Name} {control.Tag} {ExtractContent(control)}";
        if (ReadActionTokens.Any(token => descriptor.Contains(token, StringComparison.OrdinalIgnoreCase)))
            return false;

        return WriteActionTokens.Any(token => descriptor.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractContent(Control control)
    {
        return control switch
        {
            ContentControl contentControl => contentControl.Content?.ToString() ?? string.Empty,
            _ => string.Empty
        };
    }
}
