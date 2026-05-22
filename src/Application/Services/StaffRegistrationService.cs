using OS.Application.Interfaces;
using OS.Infrastructure.Security;
using OS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;

namespace OS.Application.Services;

/// <summary>
/// Paso del flujo de registro de personal.
/// </summary>
public enum RegistrationStep
{
    BasicInfo = 1,
    Password = 2,
    SecurityQuestions = 3,
    Summary = 4
}

/// <summary>
/// DTO para datos básicos del empleado (Paso 1).
/// </summary>
public class BasicInfoDto
{
    public string FullName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string? Email { get; set; }
}

/// <summary>
/// DTO para preguntas de seguridad (Paso 3).
/// </summary>
public class SecurityQuestionDto
{
    public int QuestionIndex { get; set; } // 1, 2, o 3
    public string QuestionText { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string AnswerConfirmation { get; set; } = string.Empty;
}

/// <summary>
/// Servicio de registro de personal con flujo obligatorio de 4 pasos.
/// </summary>
public interface IStaffRegistrationService
{
    /// <summary>
    /// Valida el Paso 1: Datos básicos del empleado.
    /// </summary>
    Task<(bool isValid, string[] errors)> ValidateBasicInfoAsync(BasicInfoDto dto);

    /// <summary>
    /// Valida el Paso 2: Contraseña.
    /// </summary>
    Task<(bool isValid, string message)> ValidatePasswordAsync(string password, string confirmPassword, string username);

    /// <summary>
    /// Valida el Paso 3: Preguntas de seguridad.
    /// </summary>
    Task<(bool isValid, string[] errors)> ValidateSecurityQuestionsAsync(List<SecurityQuestionDto> questions);

    /// <summary>
    /// Completa el registro de personal (todos los pasos).
    /// </summary>
    Task<Guid> CompleteRegistrationAsync(
        BasicInfoDto basicInfo,
        string password,
        List<SecurityQuestionDto> securityQuestions,
        Guid roleId,
        Guid registeredByAdminId);

    /// <summary>
    /// Obtiene las preguntas de seguridad predefinidas.
    /// </summary>
    Task<List<string>> GetPredefinedQuestionsAsync();
}

/// <summary>
/// Implementación del servicio de registro de personal.
/// </summary>
public class StaffRegistrationService : IStaffRegistrationService
{
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly ClinicDbContext _dbContext;
    private readonly IAuditService _auditService;

    public StaffRegistrationService(
        IPasswordHashingService passwordHashingService,
        ClinicDbContext dbContext,
        IAuditService auditService)
    {
        _passwordHashingService = passwordHashingService ?? throw new ArgumentNullException(nameof(passwordHashingService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>
    /// Valida el Paso 1: Datos básicos del empleado.
    /// </summary>
    public async Task<(bool isValid, string[] errors)> ValidateBasicInfoAsync(BasicInfoDto dto)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.FullName))
            errors.Add("El nombre completo es obligatorio");
        else if (dto.FullName.Length < 3)
            errors.Add("El nombre completo debe tener al menos 3 caracteres");

        if (string.IsNullOrWhiteSpace(dto.EmployeeNumber))
            errors.Add("El número de empleado es obligatorio");

        if (string.IsNullOrWhiteSpace(dto.Specialty))
            errors.Add("La especialidad/rol es obligatoria");

        // Verificar que el número de empleado no exista
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == dto.EmployeeNumber);
        if (existingUser != null)
            errors.Add("El número de empleado ya está registrado");

        return (errors.Count == 0, errors.ToArray());
    }

    /// <summary>
    /// Valida el Paso 2: Contraseña.
    /// </summary>
    public Task<(bool isValid, string message)> ValidatePasswordAsync(string password, string confirmPassword, string username)
    {
        if (string.IsNullOrWhiteSpace(password))
            return Task.FromResult((false, "La contraseña es obligatoria"));

        if (password != confirmPassword)
            return Task.FromResult((false, "Las contraseñas no coinciden"));

        // Validar fortaleza
        var strengthResult = _passwordHashingService.ValidatePasswordStrength(password);
        if (!strengthResult.IsValid)
            return Task.FromResult((false, strengthResult.Message));

        // Validar que no contenga el nombre de usuario
        if (!string.IsNullOrWhiteSpace(username) && password.Contains(username, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult((false, "La contraseña no puede contener el nombre de usuario"));

        return Task.FromResult((true, "Contraseña válida"));
    }

    /// <summary>
    /// Valida el Paso 3: Preguntas de seguridad.
    /// </summary>
    public Task<(bool isValid, string[] errors)> ValidateSecurityQuestionsAsync(List<SecurityQuestionDto> questions)
    {
        var errors = new List<string>();

        if (questions == null || questions.Count != 3)
            errors.Add("Debe configurar exactamente 3 preguntas de seguridad");

        var questionIndices = new HashSet<int>();

        foreach (var question in questions ?? new List<SecurityQuestionDto>())
        {
            if (question.QuestionIndex < 1 || question.QuestionIndex > 3)
                errors.Add($"Índice de pregunta inválido: {question.QuestionIndex}");

            if (questionIndices.Contains(question.QuestionIndex))
                errors.Add($"El índice de pregunta {question.QuestionIndex} está duplicado");
            else
                questionIndices.Add(question.QuestionIndex);

            if (string.IsNullOrWhiteSpace(question.QuestionText))
                errors.Add($"La pregunta {question.QuestionIndex} es obligatoria");

            if (string.IsNullOrWhiteSpace(question.Answer))
                errors.Add($"La respuesta {question.QuestionIndex} es obligatoria");
            else if (question.Answer.Length < 3)
                errors.Add($"La respuesta {question.QuestionIndex} debe tener al menos 3 caracteres");

            if (question.Answer != question.AnswerConfirmation)
                errors.Add($"La confirmación de respuesta {question.QuestionIndex} no coincide");
        }

        return Task.FromResult((errors.Count == 0, errors.ToArray()));
    }

    /// <summary>
    /// Completa el registro de personal (todos los pasos).
    /// </summary>
    public async Task<Guid> CompleteRegistrationAsync(
        BasicInfoDto basicInfo,
        string password,
        List<SecurityQuestionDto> securityQuestions,
        Guid roleId,
        Guid registeredByAdminId)
    {
        // Paso 1: Crear usuario
        var hashedPassword = _passwordHashingService.HashPassword(password);
        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Username = basicInfo.EmployeeNumber ?? string.Empty,
            PasswordHash = hashedPassword,
            FullName = basicInfo.FullName ?? string.Empty,
            Email = basicInfo.Email ?? string.Empty,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);

        // Paso 2: Asignar rol
        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = registeredByAdminId
        };

        _dbContext.UserRoles.Add(userRole);

        // Paso 3: Guardar historial de contraseña
        var passwordHistory = new PasswordHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PasswordHash = hashedPassword,
            ChangedAt = DateTime.UtcNow
        };

        _dbContext.PasswordHistories.Add(passwordHistory);

        // Paso 4: Guardar preguntas de seguridad
        foreach (var questionDto in securityQuestions)
        {
            var answerHash = HashSecurityAnswer(questionDto.Answer);

            var securityQuestion = new SecurityQuestion
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                QuestionIndex = questionDto.QuestionIndex,
                QuestionText = questionDto.QuestionText,
                AnswerHash = answerHash,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.SecurityQuestions.Add(securityQuestion);
        }

        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "STAFF_REGISTRATION",
            "REGISTRATION_COMPLETED",
            $"Registro de personal completado para usuario {userId} ({basicInfo.FullName}) con rol {roleId}",
            userId);

        return userId;
    }

    /// <summary>
    /// Obtiene las preguntas de seguridad predefinidas.
    /// </summary>
    public Task<List<string>> GetPredefinedQuestionsAsync()
    {
        var predefinedQuestions = new List<string>
        {
            "¿Cuál es el nombre de tu primera mascota?",
            "¿En qué ciudad naciste?",
            "¿Cuál es el nombre de tu escuela primaria?",
            "¿Cuál es tu color favorito?",
            "¿Cuál es el modelo de tu primer automóvil?",
            "¿Cuál es el nombre de tu mejor amigo de la infancia?",
            "¿Cuál es tu película favorita?",
            "¿Cuál es tu comida favorita?",
            "¿En qué año te graduaste de la secundaria?",
            "¿Cuál es el nombre de tu abuelo materno?"
        };

        return Task.FromResult(predefinedQuestions);
    }

    /// <summary>
    /// Hashea la respuesta de seguridad (SHA-256 del texto en minúsculas).
    /// </summary>
    private static string HashSecurityAnswer(string answer)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(answer.ToLowerInvariant().Trim());
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
