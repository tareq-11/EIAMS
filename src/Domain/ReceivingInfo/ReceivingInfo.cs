using Domain.Common;
using SharedKernel;

namespace Domain.ReceivingInfos;

/// <summary>The 1:1 type-specific detail (Petal) for a Receiving WarehouseDocument.</summary>
public sealed class ReceivingInfo : Entity, IAuditableEntity
{
    private ReceivingInfo() { }

    /// <summary>The entity ID is also the parent WarehouseDocument ID.</summary>
    public string SupplierRef { get; private set; }
    public string? SupplierInvoiceRef { get; private set; }
    public ReceivingType ReceivingType { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<ReceivingInfo> Create(
        Guid documentId,
        string supplierRef,
        string? supplierInvoiceRef,
        ReceivingType receivingType)
    {
        Result<(string SupplierRef, string? InvoiceRef)> validation = Validate(
            supplierRef,
            supplierInvoiceRef,
            receivingType);

        if (validation.IsFailure)
        {
            return Result.Failure<ReceivingInfo>(validation.Error);
        }

        var info = new ReceivingInfo
        {
            Id = documentId,
            SupplierRef = validation.Value.SupplierRef,
            SupplierInvoiceRef = validation.Value.InvoiceRef,
            ReceivingType = receivingType
        };

        info.Raise(new ReceivingInfoCreatedDomainEvent(documentId, receivingType));

        return info;
    }

    public Result Update(string supplierRef, string? supplierInvoiceRef, ReceivingType receivingType)
    {
        Result<(string SupplierRef, string? InvoiceRef)> validation = Validate(
            supplierRef,
            supplierInvoiceRef,
            receivingType);

        if (validation.IsFailure)
        {
            return Result.Failure(validation.Error);
        }

        if (SupplierRef == validation.Value.SupplierRef &&
            SupplierInvoiceRef == validation.Value.InvoiceRef &&
            ReceivingType == receivingType)
        {
            return Result.Success();
        }

        SupplierRef = validation.Value.SupplierRef;
        SupplierInvoiceRef = validation.Value.InvoiceRef;
        ReceivingType = receivingType;

        Raise(new ReceivingInfoUpdatedDomainEvent(Id, receivingType));

        return Result.Success();
    }

    private static Result<(string SupplierRef, string? InvoiceRef)> Validate(
        string supplierRef,
        string? supplierInvoiceRef,
        ReceivingType receivingType)
    {
        string normalizedSupplierRef = supplierRef?.Trim() ?? string.Empty;
        string? normalizedInvoiceRef = string.IsNullOrWhiteSpace(supplierInvoiceRef)
            ? null
            : supplierInvoiceRef.Trim();

        if (normalizedSupplierRef.Length is 0 or > 200)
        {
            return Result.Failure<(string, string?)>(ReceivingInfoErrors.SupplierRefInvalid);
        }

        if (normalizedInvoiceRef?.Length > 100)
        {
            return Result.Failure<(string, string?)>(ReceivingInfoErrors.SupplierInvoiceRefTooLong);
        }

        if (!Enum.IsDefined(receivingType))
        {
            return Result.Failure<(string, string?)>(ReceivingInfoErrors.ReceivingTypeInvalid);
        }

        return (normalizedSupplierRef, normalizedInvoiceRef);
    }
}
