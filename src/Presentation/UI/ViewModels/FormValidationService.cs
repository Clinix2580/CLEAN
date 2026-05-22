using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace OS.Presentation.UI.ViewModels;

/// <summary>
/// Resultado de validación de campo.
/// </summary>
public class FieldValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? WarningMessage { get; set; }
}

/// <summary>
/// Estado de validación de formulario.
/// </summary>
public class FormValidationState
{
    public bool IsFormValid { get; set; }
    public Dictionary<string, FieldValidationResult> FieldResults { get; } = new();
    public bool HasUnsavedChanges { get; set; }
}

/// <summary>
/// Servicio de validación de formularios.
/// </summary>
public interface IFormValidationService
{
    /// <summary>
    /// Valida un campo individual.
    /// </summary>
    FieldValidationResult ValidateField(string fieldName, string? value, bool isRequired, int? minLength = null, int? maxLength = null, string? regexPattern = null);

    /// <summary>
    /// Valida un campo de email.
    /// </summary>
    FieldValidationResult ValidateEmail(string fieldName, string? value, bool isRequired);

    /// <summary>
    /// Valida un campo de contraseña.
    /// </summary>
    FieldValidationResult ValidatePassword(string fieldName, string? value, bool isRequired);

    /// <summary>
    /// Valida confirmación de contraseña.
    /// </summary>
    FieldValidationResult ValidatePasswordConfirmation(string fieldName, string? value, string? originalPassword);

    /// <summary>
    /// Valida un campo numérico.
    /// </summary>
    FieldValidationResult ValidateNumeric(string fieldName, string? value, bool isRequired, decimal? minValue = null, decimal? maxValue = null);

    /// <summary>
    /// Valida un campo de fecha.
    /// </summary>
    FieldValidationResult ValidateDate(string fieldName, DateTime? value, bool isRequired, DateTime? minDate = null, DateTime? maxDate = null);
}

/// <summary>
/// Implementación del servicio de validación de formularios.
/// </summary>
public class FormValidationService : IFormValidationService
{
    /// <summary>
    /// Valida un campo individual.
    /// </summary>
    public FieldValidationResult ValidateField(string fieldName, string? value, bool isRequired, int? minLength = null, int? maxLength = null, string? regexPattern = null)
    {
        if (isRequired && string.IsNullOrWhiteSpace(value))
        {
            return new FieldValidationResult
            {
                IsValid = false,
                ErrorMessage = $"{fieldName} es obligatorio"
            };
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            if (minLength.HasValue && value.Length < minLength.Value)
            {
                return new FieldValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"{fieldName} debe tener al menos {minLength} caracteres"
                };
            }

            if (maxLength.HasValue && value.Length > maxLength.Value)
            {
                return new FieldValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"{fieldName} no puede exceder {maxLength} caracteres"
                };
            }

            if (!string.IsNullOrWhiteSpace(regexPattern))
            {
                var regex = new System.Text.RegularExpressions.Regex(regexPattern);
                if (!regex.IsMatch(value))
                {
                    return new FieldValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"{fieldName} tiene un formato inválido"
                    };
                }
            }
        }

        return new FieldValidationResult { IsValid = true };
    }

    /// <summary>
    /// Valida un campo de email.
    /// </summary>
    public FieldValidationResult ValidateEmail(string fieldName, string? value, bool isRequired)
    {
        if (isRequired && string.IsNullOrWhiteSpace(value))
        {
            return new FieldValidationResult
            {
                IsValid = false,
                ErrorMessage = $"{fieldName} es obligatorio"
            };
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            var emailAttribute = new EmailAddressAttribute();
            if (!emailAttribute.IsValid(value))
            {
                return new FieldValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"{fieldName} no tiene un formato válido"
                };
            }
        }

        return new FieldValidationResult { IsValid = true };
    }

    /// <summary>
    /// Valida un campo de contraseña.
    /// </summary>
    public FieldValidationResult ValidatePassword(string fieldName, string? value, bool isRequired)
    {
        if (isRequired && string.IsNullOrWhiteSpace(value))
        {
            return new FieldValidationResult
            {
                IsValid = false,
                ErrorMessage = $"{fieldName} es obligatorio"
            };
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            if (value.Length < 8)
            {
                return new FieldValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"{fieldName} debe tener al menos 8 caracteres"
                };
            }

            if (!value.Any(char.IsUpper))
            {
                return new FieldValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"{fieldName} debe contener al menos una mayúscula"
                };
            }

            if (!value.Any(char.IsLower))
            {
                return new FieldValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"{fieldName} debe contener al menos una minúscula"
                };
            }

            if (!value.Any(char.IsDigit))
            {
                return new FieldValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"{fieldName} debe contener al menos un número"
                };
            }
        }

        return new FieldValidationResult { IsValid = true };
    }

    /// <summary>
    /// Valida confirmación de contraseña.
    /// </summary>
    public FieldValidationResult ValidatePasswordConfirmation(string fieldName, string? value, string? originalPassword)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new FieldValidationResult
            {
                IsValid = false,
                ErrorMessage = $"{fieldName} es obligatorio"
            };
        }

        if (value != originalPassword)
        {
            return new FieldValidationResult
            {
                IsValid = false,
                ErrorMessage = $"{fieldName} no coincide con la contraseña original"
            };
        }

        return new FieldValidationResult { IsValid = true };
    }

    /// <summary>
    /// Valida un campo numérico.
    /// </summary>
    public FieldValidationResult ValidateNumeric(string fieldName, string? value, bool isRequired, decimal? minValue = null, decimal? maxValue = null)
    {
        if (isRequired && string.IsNullOrWhiteSpace(value))
        {
            return new FieldValidationResult
            {
                IsValid = false,
                ErrorMessage = $"{fieldName} es obligatorio"
            };
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            if (!decimal.TryParse(value, out var numericValue))
            {
                return new FieldValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"{fieldName} debe ser un número válido"
                };
            }

            if (minValue.HasValue && numericValue < minValue.Value)
            {
                return new FieldValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"{fieldName} debe ser al menos {minValue}"
                };
            }

            if (maxValue.HasValue && numericValue > maxValue.Value)
            {
                return new FieldValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"{fieldName} no puede exceder {maxValue}"
                };
            }
        }

        return new FieldValidationResult { IsValid = true };
    }

    /// <summary>
    /// Valida un campo de fecha.
    /// </summary>
    public FieldValidationResult ValidateDate(string fieldName, DateTime? value, bool isRequired, DateTime? minDate = null, DateTime? maxDate = null)
    {
        if (isRequired && !value.HasValue)
        {
            return new FieldValidationResult
            {
                IsValid = false,
                ErrorMessage = $"{fieldName} es obligatorio"
            };
        }

        if (value.HasValue)
        {
            if (minDate.HasValue && value.Value < minDate.Value)
            {
                return new FieldValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"{fieldName} debe ser posterior a {minDate.Value:yyyy-MM-dd}"
                };
            }

            if (maxDate.HasValue && value.Value > maxDate.Value)
            {
                return new FieldValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"{fieldName} debe ser anterior a {maxDate.Value:yyyy-MM-dd}"
                };
            }
        }

        return new FieldValidationResult { IsValid = true };
    }
}
