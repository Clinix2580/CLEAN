using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OS.Application.DTOs;
using OS.Application.Interfaces;

namespace OS.Presentation.UI.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoggingIn;

    [ObservableProperty]
    private bool _hasError;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task Login()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            #if LANG_ES
            ErrorMessage = "Usuario y contraseña requeridos.";
            #else
            ErrorMessage = "Username and password are required.";
            #endif
            HasError = true;
            return;
        }

        IsLoggingIn = true;
        HasError = false;

        try
        {
            var result = await _authService.LoginAsync(new LoginDto
            {
                Username = Username,
                Password = Password
            });

            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "Invalid credentials";
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoggingIn = false;
        }
    }
}
