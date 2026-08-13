namespace Application.Abstractions.Data;

/// <summary>
/// Classifies provider-specific persistence exceptions without leaking the database provider into Application.
/// </summary>
public interface IDatabaseExceptionClassifier
{
    /// <summary>
    /// Determines whether an exception was caused by a database unique-constraint violation.
    /// </summary>
    /// <param name="exception">The exception raised while persisting changes.</param>
    /// <returns><see langword="true"/> when the database rejected a duplicate unique value.</returns>
    bool IsUniqueConstraintViolation(Exception exception);

    /// <summary>
    /// Determines whether an exception was caused by a specific database unique constraint or
    /// unique index.
    /// </summary>
    bool IsUniqueConstraintViolation(Exception exception, string constraintName);
}
