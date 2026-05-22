using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace OS.Presentation.UI.Converters;

/// <summary>
/// Convierte un mensaje de validación de contraseña en un color indicador.
/// </summary>
public class PasswordStrengthColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string message)
        {
            if (message.Contains("Contraseña válida"))
                return new SolidColorBrush(Colors.Green);
            
            if (message.Contains("débil"))
                return new SolidColorBrush(Colors.Red);
            
            return new SolidColorBrush(Colors.Orange);
        }
        
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convierte un mensaje de validación de contraseña en un nivel de fortaleza (0-100).
/// </summary>
public class PasswordStrengthLevelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string message)
        {
            if (message.Contains("Contraseña válida"))
                return 100;
            
            // Contar cuántos requisitos faltan
            var missingRequirements = 0;
            if (message.Contains("12 caracteres")) missingRequirements++;
            if (message.Contains("mayúscula")) missingRequirements++;
            if (message.Contains("minúscula")) missingRequirements++;
            if (message.Contains("número")) missingRequirements++;
            if (message.Contains("símbolo")) missingRequirements++;
            
            // Calcular porcentaje basado en requisitos cumplidos
            var totalRequirements = 5;
            var completedRequirements = totalRequirements - missingRequirements;
            return (completedRequirements * 100) / totalRequirements;
        }
        
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convierte un booleano en visibilidad.
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        
        return false;
    }
}
