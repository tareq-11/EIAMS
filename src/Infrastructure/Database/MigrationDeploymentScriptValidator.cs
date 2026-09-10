namespace Infrastructure.Database;

/// <summary>
/// Validates the limited deployment-script invariants required by the EIAMS concurrent-index
/// migration. This is a guard for reviewed, version-bounded scripts; it does not execute SQL.
/// </summary>
public static class MigrationDeploymentScriptValidator
{
    private const string ConcurrentIndexStatement = "CREATE INDEX CONCURRENTLY";
    private const string EfIdempotentDoWrapper = "DO $EF$";
    private const string TrigramMigrationHistoryInsert =
        "VALUES ('20260901160000_AddTrigramSearchIndexes'";

    /// <summary>
    /// Rejects an EF idempotent wrapper around concurrent DDL and verifies that the known
    /// trigram migration records its EF history only after its concurrent index commands.
    /// </summary>
    /// <param name="script">A generated EF Core deployment script.</param>
    /// <exception cref="ArgumentException">Thrown when the script is blank.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the script is unsafe for deployment.</exception>
    public static void ValidateConcurrentIndexDeploymentScript(string script)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);

        int firstConcurrentIndex = script.IndexOf(ConcurrentIndexStatement, StringComparison.OrdinalIgnoreCase);
        if (firstConcurrentIndex < 0)
        {
            throw new InvalidOperationException("The reviewed deployment script does not contain the expected concurrent index migration.");
        }

        if (script.Contains(EfIdempotentDoWrapper, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "EF idempotent scripts are not deployable when this migration chain contains CREATE INDEX CONCURRENTLY. " +
                "Generate and review a version-bounded non-idempotent script instead.");
        }

        int lastTransactionStartBeforeConcurrentIndex = script.LastIndexOf(
            "START TRANSACTION;",
            firstConcurrentIndex,
            StringComparison.OrdinalIgnoreCase);
        int lastCommitBeforeConcurrentIndex = script.LastIndexOf(
            "COMMIT;",
            firstConcurrentIndex,
            StringComparison.OrdinalIgnoreCase);
        if (lastCommitBeforeConcurrentIndex < 0 || lastCommitBeforeConcurrentIndex < lastTransactionStartBeforeConcurrentIndex)
        {
            throw new InvalidOperationException(
                "CREATE INDEX CONCURRENTLY is still inside an open transaction in the reviewed deployment script.");
        }

        int lastConcurrentIndex = script.LastIndexOf(ConcurrentIndexStatement, StringComparison.OrdinalIgnoreCase);
        int historyInsert = script.IndexOf(TrigramMigrationHistoryInsert, StringComparison.Ordinal);
        if (historyInsert < 0 || historyInsert <= lastConcurrentIndex)
        {
            throw new InvalidOperationException(
                "The trigram migration history insert must occur after every concurrent index command.");
        }
    }
}
