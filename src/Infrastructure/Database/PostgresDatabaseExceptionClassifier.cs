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
}
