using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace OS.Presentation.UI.Accessibility;

/// <summary>
/// Validador de accesibilidad según NOM-024-SSA1-2012.
/// Verifica contraste de colores, tamaño de fuente, navegación por teclado y otros requisitos.
/// </summary>
public static class AccessibilityValidator
{
    /// <summary>
    /// Verifica si el contraste de colores cumple con WCAG AA (4.5:1 para texto normal, 3:1 para texto grande).
    /// </summary>
    public static bool ValidateColorContrast(Color foreground, Color background, bool isLargeText = false)
    {
        var ratio = CalculateContrastRatio(foreground, background);
        var minimumRatio = isLargeText ? 3.0 : 4.5;
        return ratio >= minimumRatio;
    }

    /// <summary>
    /// Calcula el ratio de contraste entre dos colores según WCAG 2.0.
    /// </summary>
    public static double CalculateContrastRatio(Color foreground, Color background)
    {
        var l1 = CalculateRelativeLuminance(foreground);
        var l2 = CalculateRelativeLuminance(background);
        
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Calcula la luminancia relativa de un color según WCAG 2.0.
    /// </summary>
    private static double CalculateRelativeLuminance(Color color)
    {
        var r = NormalizeColorComponent(color.R);
        var g = NormalizeColorComponent(color.G);
        var b = NormalizeColorComponent(color.B);
        
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    /// <summary>
    /// Normaliza un componente de color (0-255) a sRGB (0-1).
    /// </summary>
    private static double NormalizeColorComponent(byte component)
    {
        var normalized = component / 255.0;
        
        if (normalized <= 0.03928)
        {
            return normalized / 12.92;
        }
        else
        {
            return Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }
    }

    /// <summary>
    /// Verifica si el tamaño de fuente cumple con NOM-024 (mínimo 12pt para contenido principal).
    /// </summary>
    public static bool ValidateFontSize(double fontSize, bool isContent = true)
    {
        var minimumSize = isContent ? 12.0 : 10.0;
        return fontSize >= minimumSize;
    }

    /// <summary>
    /// Verifica si un control tiene etiqueta descriptiva para lectores de pantalla.
    /// </summary>
    public static bool ValidateControlLabel(FrameworkElement control)
    {
        if (control is TextBox textBox)
        {
            return !string.IsNullOrWhiteSpace(textBox.Name) || 
                   !string.IsNullOrWhiteSpace(AutomationProperties.GetName(textBox));
        }
        
        if (control is Button button)
        {
            return !string.IsNullOrWhiteSpace(button.Content?.ToString());
        }
        
        if (control is CheckBox checkBox)
        {
            return !string.IsNullOrWhiteSpace(checkBox.Content?.ToString());
        }
        
        return !string.IsNullOrWhiteSpace(AutomationProperties.GetName(control));
    }

    /// <summary>
    /// Verifica si el control es navegable por teclado.
    /// </summary>
    public static bool ValidateKeyboardNavigation(FrameworkElement control)
    {
        if (control is Control ctrl)
        {
            return ctrl.IsTabStop || ctrl.Focusable;
        }
        return control.Focusable;
    }

    /// <summary>
    /// Genera un reporte de accesibilidad para un control.
    /// </summary>
    public static AccessibilityReport GenerateReport(FrameworkElement control)
    {
        var report = new AccessibilityReport
        {
            ControlName = control.Name ?? control.GetType().Name,
            ControlType = control.GetType().Name
        };

        // Validar contraste de colores
        if (control is TextBlock textBlock)
        {
            var foreground = textBlock.Foreground as SolidColorBrush;
            var background = textBlock.Background as SolidColorBrush;
            
            if (foreground != null && background != null)
            {
                report.ContrastRatio = CalculateContrastRatio(foreground.Color, background.Color);
                report.ContrastValid = ValidateColorContrast(foreground.Color, background.Color);
            }
            
            report.FontSize = textBlock.FontSize;
            report.FontSizeValid = ValidateFontSize(textBlock.FontSize);
        }

        // Validar etiqueta
        report.HasLabel = ValidateControlLabel(control);

        // Validar navegación por teclado
        report.KeyboardNavigable = ValidateKeyboardNavigation(control);

        return report;
    }

    /// <summary>
    /// Genera un reporte de accesibilidad para una ventana completa.
    /// </summary>
    public static List<AccessibilityReport> GenerateWindowReport(Window window)
    {
        var reports = new List<AccessibilityReport>();
        
        if (window.Content is FrameworkElement content)
        {
            TraverseVisualTree(content, reports);
        }
        
        return reports;
    }

    /// <summary>
    /// Recorre el árbol visual recursivamente para generar reportes.
    /// </summary>
    private static void TraverseVisualTree(DependencyObject element, List<AccessibilityReport> reports)
    {
        if (element is FrameworkElement control)
        {
            var report = GenerateReport(control);
            reports.Add(report);
        }

        var childrenCount = VisualTreeHelper.GetChildrenCount(element);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            TraverseVisualTree(child, reports);
        }
    }
}

/// <summary>
/// Reporte de validación de accesibilidad.
/// </summary>
public class AccessibilityReport
{
    /// <summary>
    /// Nombre del control.
    /// </summary>
    public string ControlName { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de control.
    /// </summary>
    public string ControlType { get; set; } = string.Empty;

    /// <summary>
    /// Ratio de contraste calculado.
    /// </summary>
    public double ContrastRatio { get; set; }

    /// <summary>
    /// Indica si el contraste cumple con los estándares.
    /// </summary>
    public bool ContrastValid { get; set; }

    /// <summary>
    /// Tamaño de fuente.
    /// </summary>
    public double FontSize { get; set; }

    /// <summary>
    /// Indica si el tamaño de fuente cumple con los estándares.
    /// </summary>
    public bool FontSizeValid { get; set; }

    /// <summary>
    /// Indica si el control tiene etiqueta descriptiva.
    /// </summary>
    public bool HasLabel { get; set; }

    /// <summary>
    /// Indica si el control es navegable por teclado.
    /// </summary>
    public bool KeyboardNavigable { get; set; }

    /// <summary>
    /// Indica si el control cumple con todos los requisitos de accesibilidad.
    /// </summary>
    public bool IsCompliant => ContrastValid && FontSizeValid && HasLabel && KeyboardNavigable;

    /// <summary>
    /// Lista de problemas encontrados.
    /// </summary>
    public List<string> Issues
    {
        get
        {
            var issues = new List<string>();
            
            if (!ContrastValid)
                issues.Add($"Contraste insuficiente: {ContrastRatio:F2}:1 (mínimo 4.5:1)");
            
            if (!FontSizeValid)
                issues.Add($"Tamaño de fuente insuficiente: {FontSize:F1}pt (mínimo 12pt)");
            
            if (!HasLabel)
                issues.Add("Falta etiqueta descriptiva para lectores de pantalla");
            
            if (!KeyboardNavigable)
                issues.Add("Control no navegable por teclado");
            
            return issues;
        }
    }
}
