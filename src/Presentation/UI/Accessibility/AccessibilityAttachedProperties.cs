using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace OS.Presentation.UI.Accessibility;

/// <summary>
/// Propiedades adjuntas para accesibilidad según NOM-024-SSA1-2012.
/// Permite aplicar validaciones y configuraciones de accesibilidad en controles WPF.
/// </summary>
public static class AccessibilityAttachedProperties
{
    /// <summary>
    /// Identifica si el control debe validarse para accesibilidad.
    /// </summary>
    public static readonly DependencyProperty ValidateAccessibilityProperty =
        DependencyProperty.RegisterAttached(
            "ValidateAccessibility",
            typeof(bool),
            typeof(AccessibilityAttachedProperties),
            new PropertyMetadata(false, OnValidateAccessibilityChanged));

    /// <summary>
    /// Identifica si el control es texto grande (requiere contraste mínimo 3:1).
    /// </summary>
    public static readonly DependencyProperty IsLargeTextProperty =
        DependencyProperty.RegisterAttached(
            "IsLargeText",
            typeof(bool),
            typeof(AccessibilityAttachedProperties),
            new PropertyMetadata(false));

    /// <summary>
    /// Identifica el tamaño de fuente mínimo requerido.
    /// </summary>
    public static readonly DependencyProperty MinimumFontSizeProperty =
        DependencyProperty.RegisterAttached(
            "MinimumFontSize",
            typeof(double),
            typeof(AccessibilityAttachedProperties),
            new PropertyMetadata(12.0));

    /// <summary>
    /// Identifica si el control debe tener navegación por teclado.
    /// </summary>
    public static readonly DependencyProperty RequireKeyboardNavigationProperty =
        DependencyProperty.RegisterAttached(
            "RequireKeyboardNavigation",
            typeof(bool),
            typeof(AccessibilityAttachedProperties),
            new PropertyMetadata(true));

    /// <summary>
    /// Identifica la etiqueta descriptiva para lectores de pantalla.
    /// </summary>
    public static readonly DependencyProperty AccessibilityLabelProperty =
        DependencyProperty.RegisterAttached(
            "AccessibilityLabel",
            typeof(string),
            typeof(AccessibilityAttachedProperties),
            new PropertyMetadata(string.Empty, OnAccessibilityLabelChanged));

    /// <summary>
    /// Identifica el color de fondo para validación de contraste.
    /// </summary>
    public static readonly DependencyProperty BackgroundColorProperty =
        DependencyProperty.RegisterAttached(
            "BackgroundColor",
            typeof(Color),
            typeof(AccessibilityAttachedProperties),
            new PropertyMetadata(Colors.White));

    /// <summary>
    /// Identifica el color de primer plano para validación de contraste.
    /// </summary>
    public static readonly DependencyProperty ForegroundColorProperty =
        DependencyProperty.RegisterAttached(
            "ForegroundColor",
            typeof(Color),
            typeof(AccessibilityAttachedProperties),
            new PropertyMetadata(Colors.Black));

    /// <summary>
    /// Obtiene si el control debe validarse para accesibilidad.
    /// </summary>
    public static bool GetValidateAccessibility(DependencyObject obj)
    {
        return (bool)obj.GetValue(ValidateAccessibilityProperty);
    }

    /// <summary>
    /// Establece si el control debe validarse para accesibilidad.
    /// </summary>
    public static void SetValidateAccessibility(DependencyObject obj, bool value)
    {
        obj.SetValue(ValidateAccessibilityProperty, value);
    }

    /// <summary>
    /// Obtiene si el control es texto grande.
    /// </summary>
    public static bool GetIsLargeText(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsLargeTextProperty);
    }

    /// <summary>
    /// Establece si el control es texto grande.
    /// </summary>
    public static void SetIsLargeText(DependencyObject obj, bool value)
    {
        obj.SetValue(IsLargeTextProperty, value);
    }

    /// <summary>
    /// Obtiene el tamaño de fuente mínimo requerido.
    /// </summary>
    public static double GetMinimumFontSize(DependencyObject obj)
    {
        return (double)obj.GetValue(MinimumFontSizeProperty);
    }

    /// <summary>
    /// Establece el tamaño de fuente mínimo requerido.
    /// </summary>
    public static void SetMinimumFontSize(DependencyObject obj, double value)
    {
        obj.SetValue(MinimumFontSizeProperty, value);
    }

    /// <summary>
    /// Obtiene si el control requiere navegación por teclado.
    /// </summary>
    public static bool GetRequireKeyboardNavigation(DependencyObject obj)
    {
        return (bool)obj.GetValue(RequireKeyboardNavigationProperty);
    }

    /// <summary>
    /// Establece si el control requiere navegación por teclado.
    /// </summary>
    public static void SetRequireKeyboardNavigation(DependencyObject obj, bool value)
    {
        obj.SetValue(RequireKeyboardNavigationProperty, value);
    }

    /// <summary>
    /// Obtiene la etiqueta de accesibilidad.
    /// </summary>
    public static string GetAccessibilityLabel(DependencyObject obj)
    {
        return (string)obj.GetValue(AccessibilityLabelProperty);
    }

    /// <summary>
    /// Establece la etiqueta de accesibilidad.
    /// </summary>
    public static void SetAccessibilityLabel(DependencyObject obj, string value)
    {
        obj.SetValue(AccessibilityLabelProperty, value);
    }

    /// <summary>
    /// Obtiene el color de fondo.
    /// </summary>
    public static Color GetBackgroundColor(DependencyObject obj)
    {
        return (Color)obj.GetValue(BackgroundColorProperty);
    }

    /// <summary>
    /// Establece el color de fondo.
    /// </summary>
    public static void SetBackgroundColor(DependencyObject obj, Color value)
    {
        obj.SetValue(BackgroundColorProperty, value);
    }

    /// <summary>
    /// Obtiene el color de primer plano.
    /// </summary>
    public static Color GetForegroundColor(DependencyObject obj)
    {
        return (Color)obj.GetValue(ForegroundColorProperty);
    }

    /// <summary>
    /// Establece el color de primer plano.
    /// </summary>
    public static void SetForegroundColor(DependencyObject obj, Color value)
    {
        obj.SetValue(ForegroundColorProperty, value);
    }

    private static void OnValidateAccessibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        if ((bool)e.NewValue)
        {
            ApplyAccessibilityValidation(element);
        }
    }

    private static void OnAccessibilityLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element)
        {
            var label = (string)e.NewValue;
            if (!string.IsNullOrWhiteSpace(label))
            {
                AutomationProperties.SetName(element, label);
            }
        }
    }

    private static void ApplyAccessibilityValidation(FrameworkElement element)
    {
        // Validar y aplicar tamaño de fuente mínimo
        var minFontSize = GetMinimumFontSize(element);
        if (element is TextBlock textBlock)
        {
            if (textBlock.FontSize < minFontSize)
            {
                textBlock.FontSize = minFontSize;
            }
        }
        else if (element is Control control)
        {
            if (control.FontSize < minFontSize)
            {
                control.FontSize = minFontSize;
            }
        }

        // Validar contraste de colores
        var backgroundColor = GetBackgroundColor(element);
        var foregroundColor = GetForegroundColor(element);
        var isLargeText = GetIsLargeText(element);

        if (!AccessibilityValidator.ValidateColorContrast(foregroundColor, backgroundColor, isLargeText))
        {
            // Ajustar colores para cumplir con contraste mínimo
            var adjustedColors = AdjustColorsForContrast(foregroundColor, backgroundColor, isLargeText);
            SetForegroundColor(element, adjustedColors.foreground);
            SetBackgroundColor(element, adjustedColors.background);

            if (element is TextBlock tb)
            {
                tb.Foreground = new SolidColorBrush(adjustedColors.foreground);
                tb.Background = new SolidColorBrush(adjustedColors.background);
            }
            else if (element is Control ctrl)
            {
                ctrl.Foreground = new SolidColorBrush(adjustedColors.foreground);
                ctrl.Background = new SolidColorBrush(adjustedColors.background);
            }
        }

        // Validar navegación por teclado
        var requireKeyboardNav = GetRequireKeyboardNavigation(element);
        if (requireKeyboardNav)
        {
            if (element is Control ctrl)
            {
                ctrl.IsTabStop = true;
            }
            element.Focusable = true;
        }

        // Validar etiqueta de accesibilidad
        var accessibilityLabel = GetAccessibilityLabel(element);
        if (!string.IsNullOrWhiteSpace(accessibilityLabel))
        {
            AutomationProperties.SetName(element, accessibilityLabel);
        }
        else if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(element)))
        {
            // Generar etiqueta automática si no existe
            var autoLabel = GenerateAccessibilityLabel(element);
            if (!string.IsNullOrWhiteSpace(autoLabel))
            {
                AutomationProperties.SetName(element, autoLabel);
            }
        }
    }

    private static (Color foreground, Color background) AdjustColorsForContrast(Color foreground, Color background, bool isLargeText)
    {
        var ratio = AccessibilityValidator.CalculateContrastRatio(foreground, background);
        var minimumRatio = isLargeText ? 3.0 : 4.5;

        if (ratio >= minimumRatio)
            return (foreground, background);

        // Si el fondo es claro, oscurecer el primer plano
        if (background.R > 128 && background.G > 128 && background.B > 128)
        {
            return (Colors.Black, background);
        }
        else
        {
            return (Colors.White, background);
        }
    }

    private static string? GenerateAccessibilityLabel(FrameworkElement element)
    {
        if (element is Button button)
        {
            return button.Content?.ToString();
        }

        if (element is TextBox textBox)
        {
            return textBox.Name?.Replace("TextBox", "").Replace("Txt", "") + " input";
        }

        if (element is CheckBox checkBox)
        {
            return checkBox.Content?.ToString();
        }

        if (element is ComboBox comboBox)
        {
            return comboBox.Name?.Replace("ComboBox", "").Replace("Cbo", "") + " dropdown";
        }

        return element.Name;
    }
}
