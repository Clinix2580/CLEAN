using System.Collections.ObjectModel;

namespace OS.Domain.Exceptions;

/// <summary>
/// Base exception for domain layer errors.
/// Represents validation or business logic failures.
/// </summary>
public class DomainException : Exception
{
    /// <summary>
    /// Initializes a new instance of the DomainException class.
    /// </summary>
    public DomainException() : base() { }

    /// <summary>
    /// Initializes a new instance of the DomainException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DomainException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the DomainException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="inner">The inner exception.</param>
    public DomainException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Exception raised when an entity is not found.
/// </summary>
public class NotFoundException : DomainException
{
    /// <summary>
    /// Gets the type name of the entity not found.
    /// </summary>
    public string EntityName { get; }

    /// <summary>
    /// Gets the ID of the entity that was not found.
    /// </summary>
    public Guid EntityId { get; }

    /// <summary>
    /// Initializes a new instance of the NotFoundException class.
    /// </summary>
    public NotFoundException() : base()
    {
        EntityName = string.Empty;
        EntityId = Guid.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the NotFoundException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public NotFoundException(string message) : base(message)
    {
        EntityName = string.Empty;
        EntityId = Guid.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the NotFoundException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="inner">The inner exception.</param>
    public NotFoundException(string message, Exception inner) : base(message, inner)
    {
        EntityName = string.Empty;
        EntityId = Guid.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the NotFoundException class.
    /// </summary>
    /// <param name="entityName">The name of the entity.</param>
    /// <param name="id">The ID of the entity.</param>
    public NotFoundException(string entityName, Guid id)
        : base($"Entity {entityName} with id {id} was not found.")
    {
        EntityName = entityName;
        EntityId = id;
    }
}

/// <summary>
/// Exception raised when domain validation rules are violated.
/// </summary>
public class ValidationException : DomainException
{
    /// <summary>
    /// Gets the list of validation error messages.
    /// </summary>
    public Collection<string> Errors { get; }

    /// <summary>
    /// Initializes a new instance of the ValidationException class.
    /// </summary>
    public ValidationException() : base()
    {
        Errors = new Collection<string>();
    }

    /// <summary>
    /// Initializes a new instance of the ValidationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ValidationException(string message) : base(message)
    {
        Errors = new Collection<string>();
    }

    /// <summary>
    /// Initializes a new instance of the ValidationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="inner">The inner exception.</param>
    public ValidationException(string message, Exception inner) : base(message, inner)
    {
        Errors = new Collection<string>();
    }

    /// <summary>
    /// Initializes a new instance of the ValidationException class.
    /// </summary>
    /// <param name="errors">The validation error messages.</param>
    public ValidationException(IEnumerable<string> errors)
        : base("Validation failed: " + string.Join(", ", errors))
    {
        Errors = new Collection<string>(errors.ToList());
    }

    /// <summary>
    /// Initializes a new instance of the ValidationException class.
    /// </summary>
    /// <param name="propertyName">The name of the property that failed validation.</param>
    /// <param name="errorMessage">The validation error message.</param>
    public ValidationException(string propertyName, string errorMessage)
        : base($"Validation failed for property '{propertyName}': {errorMessage}")
    {
        Errors = new Collection<string> { errorMessage };
    }
}

/// <summary>
/// Exception raised when an unauthorized operation is attempted.
/// </summary>
public class UnauthorizedException : DomainException
{
    /// <summary>
    /// Initializes a new instance of the UnauthorizedException class.
    /// </summary>
    public UnauthorizedException() : base("Unauthorized access.") { }

    /// <summary>
    /// Initializes a new instance of the UnauthorizedException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public UnauthorizedException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the UnauthorizedException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="inner">The inner exception.</param>
    public UnauthorizedException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Exception raised when a compliance rule is violated.
/// </summary>
public class ComplianceException : DomainException
{
    /// <summary>
    /// Gets the compliance rule that was violated.
    /// </summary>
    public string? ComplianceRule { get; }

    /// <summary>
    /// Initializes a new instance of the ComplianceException class.
    /// </summary>
    public ComplianceException() : base() { }

    /// <summary>
    /// Initializes a new instance of the ComplianceException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ComplianceException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the ComplianceException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="inner">The inner exception.</param>
    public ComplianceException(string message, Exception inner) : base(message, inner) { }

    /// <summary>
    /// Initializes a new instance of the ComplianceException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="complianceRule">The compliance rule that was violated.</param>
    public ComplianceException(string message, string complianceRule) : base(message)
    {
        ComplianceRule = complianceRule;
    }
}

/// <summary>
/// Exception raised when a business rule is violated.
/// </summary>
public class BusinessRuleViolationException : DomainException
{
    /// <summary>
    /// Gets the business rule that was violated.
    /// </summary>
    public string? BusinessRule { get; }

    /// <summary>
    /// Initializes a new instance of the BusinessRuleViolationException class.
    /// </summary>
    public BusinessRuleViolationException() : base() { }

    /// <summary>
    /// Initializes a new instance of the BusinessRuleViolationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public BusinessRuleViolationException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the BusinessRuleViolationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="inner">The inner exception.</param>
    public BusinessRuleViolationException(string message, Exception inner) : base(message, inner) { }

    /// <summary>
    /// Initializes a new instance of the BusinessRuleViolationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="businessRule">The business rule that was violated.</param>
    public BusinessRuleViolationException(string message, string businessRule) : base(message)
    {
        BusinessRule = businessRule;
    }
}

/// <summary>
/// Exception raised when an entity is in an invalid state for the operation.
/// </summary>
public class InvalidStateException : DomainException
{
    /// <summary>
    /// Initializes a new instance of the InvalidStateException class.
    /// </summary>
    public InvalidStateException() : base() { }

    /// <summary>
    /// Initializes a new instance of the InvalidStateException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public InvalidStateException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the InvalidStateException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="inner">The inner exception.</param>
    public InvalidStateException(string message, Exception inner) : base(message, inner) { }
}
