using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Database;

internal sealed class PostgresDatabaseExceptionClassifier : IDatabaseExceptionClassifier
{
    public bool IsUniqueConstraintViolation(Exception exception) =>
        exception is DbUpdateException
        {
            InnerException: PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            }
        };

    public bool IsUniqueConstraintViolation(Exception exception, string constraintName) =>
        exception is DbUpdateException
        {
            InnerException: PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: var actualConstraintName
            }
        } && string.Equals(actualConstraintName, constraintName, StringComparison.Ordinal);
}
