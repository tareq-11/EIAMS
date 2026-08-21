using Domain.Common;
using SharedKernel;

namespace Domain.Custodies;

public static class CustodyErrors
{
    public static Error NotFound(Guid custodyId) => Error.NotFound("Custodies.NotFound", "Custody was not found.", new { custody_id = custodyId });
    public static Error NoActiveCustody(Guid assetId) => Error.Problem("Custodies.NoActiveCustody", "Asset does not have an active custody.", new { asset_id = assetId });
    public static Error ActiveCustodyExists(Guid assetId) => Error.Conflict("Custodies.ActiveCustodyExists", "Asset already has an active custody.", new { asset_id = assetId });
    public static Error NotOperational(Guid custodyId) => Error.Problem("Custodies.NotOperational", "Custody is not operational.", new { custody_id = custodyId });
    public static Error HolderNotFound(PartyType holderType, Guid holderId) => Error.Problem("Custodies.HolderNotFound", "Custody holder does not exist.", new { holder_type = holderType.ToString(), holder_id = holderId });
    public static Error HolderInactive(PartyType holderType, Guid holderId) => Error.Problem("Custodies.HolderInactive", "Custody holder is inactive.", new { holder_type = holderType.ToString(), holder_id = holderId });
    public static readonly Error ExternalHolderNotSupported = Error.Problem("Custodies.ExternalHolderNotSupported", "External holders are not supported until an active external-party master is available.");
    public static Error RowVersionMismatch(Guid custodyId, int expected, int? current) => Error.Conflict("Custodies.RowVersionMismatch", "Custody was modified by another request.", new { custody_id = custodyId, expected_row_version = expected, current_row_version = current });
    public static Error CannotReverseChangedCustody(Guid custodyId) => Error.Conflict("Custodies.CannotReverseChangedCustody", "Custody changed after the original operation and cannot be reversed.", new { custody_id = custodyId });
    public static readonly Error IdentityRequired = Error.Problem("Custodies.IdentityRequired", "Custody identity values are required.");
    public static readonly Error HolderTypeInvalid = Error.Problem("Custodies.HolderTypeInvalid", "HolderType must be a known value.");
    public static readonly Error CustodyKindInvalid = Error.Problem("Custodies.CustodyKindInvalid", "CustodyKind must be a known value.");
    public static readonly Error StatusInvalid = Error.Problem("Custodies.StatusInvalid", "CustodyStatus must be a known value.");
    public static readonly Error PersonalRequiresEmployee = Error.Problem("Custodies.PersonalRequiresEmployee", "Personal custody requires an Employee holder.");
    public static readonly Error NotActive = Error.Problem("Custodies.NotActive", "Only an active custody can be closed.");
    public static readonly Error CloseTimeInvalid = Error.Problem("Custodies.CloseTimeInvalid", "Custody close time must be after its open time.");
    public static readonly Error ReturnDocumentRequired = Error.Problem("Custodies.ReturnDocumentRequired", "A return document is required when closing custody for a return.");
    public static readonly Error DisposalDocumentRequired = Error.Problem("Custodies.DisposalDocumentRequired", "A disposal document is required when closing custody for disposal.");
}
