namespace Application.Abstractions.Audit;

public enum AuditOperationKind
{
    Http = 0,
    Anonymous = 1,
    Background = 2,
    System = 3,
}
