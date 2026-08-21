using SharedKernel;

namespace Domain.CustodyHistories;

public static class CustodyHistoryErrors
{
    public static readonly Error IdentityRequired = Error.Problem("CustodyHistories.IdentityRequired", "Custody history identity values are required.");
    public static readonly Error StatusInvalid = Error.Problem("CustodyHistories.StatusInvalid", "Custody history statuses must be known values.");
    public static readonly Error TransitionInvalid = Error.Problem("CustodyHistories.TransitionInvalid", "Custody history must record an actual status transition.");
    public static readonly Error NoteTooLong = Error.Problem("CustodyHistories.NoteTooLong", "Custody history note cannot exceed 300 characters.");
}
