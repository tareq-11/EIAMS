namespace Application.Abstractions.Searching;

/// <summary>
/// Builds a literal, contains-style SQL LIKE pattern for user-provided search text.
/// </summary>
/// <remarks>
/// PostgreSQL treats <c>%</c> and <c>_</c> as pattern operators.  Search endpoints
/// use this helper with the matching <c>ESCAPE '\\'</c> clause so those characters
/// remain literal user input rather than widening a result set.
/// </remarks>
public static class SqlLikePattern
{
    /// <summary>
    /// The explicit escape character supplied to EF Core LIKE/ILIKE translations.
    /// </summary>
    public const string EscapeCharacter = "\\";

    /// <summary>
    /// Creates a pattern that finds a literal substring, or <see langword="null"/>
    /// when the input is empty or whitespace.
    /// </summary>
    public static string? CreateContains(string? value, bool normalizeToUpper = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string term = value.Trim();
        if (normalizeToUpper)
        {
            term = term.ToUpperInvariant();
        }

        string escaped = term
            .Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
            .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }
}
