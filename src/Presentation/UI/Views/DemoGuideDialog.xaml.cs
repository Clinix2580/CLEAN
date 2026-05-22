using System.Windows;
using System.Windows.Controls;

namespace OS.Presentation.UI.Views;

/// <summary>
/// Diálogo para mostrar guía interactiva en modo Demo.
/// Muestra descripciones de módulos y funciones en el idioma activo (ES/EN).
/// </summary>
public partial class DemoGuideDialog : Window
{
    public DemoGuideDialog(string description, string moduleName, string functionName)
    {
        InitializeComponent();
        
        // Establecer el título según el idioma
#if LANG_ES
        Title = "Guía de Modo Demo";
#else
        Title = "Demo Mode Guide";
#endif

        // Establecer el contenido
        ModuleNameText.Text = moduleName;
        DescriptionText.Text = description;

        // Mostrar información de función si está disponible
        if (!string.IsNullOrEmpty(functionName))
        {
            FunctionNameText.Text = functionName;
            FunctionInfoBorder.Visibility = Visibility.Visible;
        }

        // Actualizar textos según el idioma
#if LANG_ES
        ((TextBlock)FunctionInfoBorder.Child).Text = "Función:";
        ((TextBlock)((Border)((StackPanel)Content).Children[4]).Child).Text = "Descripción:";
        ((TextBlock)((Border)((StackPanel)Content).Children[5]).Child).Text = "🔒 Esta función es de solo lectura en Modo Demo.";
        ((Button)((StackPanel)Content).Children[6]).Content = "Cerrar";
#else
        ((TextBlock)FunctionInfoBorder.Child).Text = "Function:";
        ((TextBlock)((Border)((StackPanel)Content).Children[4]).Child).Text = "Description:";
        ((TextBlock)((Border)((StackPanel)Content).Children[5]).Child).Text = "🔒 This feature is read-only in Demo Mode.";
        ((Button)((StackPanel)Content).Children[6]).Content = "Close";
#endif
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
