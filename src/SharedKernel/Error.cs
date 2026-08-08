namespace SharedKernel;

public record Error
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
    public static readonly Error NullValue = new(
        "General.Null",
        "Null value was provided",
        ErrorType.Failure);

    public Error(string code, string description, ErrorType type, object? details = null)
    {
        Code = code;
        Description = description;
        Type = type;
        Details = details;
    }

    public string Code { get; }

    public string Description { get; }

    public ErrorType Type { get; }

    public object? Details { get; }

    public static Error Failure(string code, string description, object? details = null) =>
        new(code, description, ErrorType.Failure, details);

    public static Error NotFound(string code, string description, object? details = null) =>
        new(code, description, ErrorType.NotFound, details);

    public static Error Problem(string code, string description, object? details = null) =>
        new(code, description, ErrorType.Problem, details);

    public static Error Conflict(string code, string description, object? details = null) =>
        new(code, description, ErrorType.Conflict, details);

    public static Error Forbidden(string code, string description, object? details = null) =>
        new(code, description, ErrorType.Forbidden, details);
}
