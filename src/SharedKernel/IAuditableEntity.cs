namespace SharedKernel;

public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; }
    DateTime? UpdatedAtUtc { get; }
    Guid? CreatedBy { get; }
    Guid? UpdatedBy { get; }
}
