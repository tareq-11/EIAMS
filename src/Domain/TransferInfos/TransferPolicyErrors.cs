using SharedKernel;

namespace Domain.TransferInfos;

public static class TransferPolicyErrors
{
    public static Error CrossGovernorateBlocked(
        Guid sourceWarehouseId,
        Guid destinationWarehouseId,
        string sourceGovernorateCode,
        string destinationGovernorateCode) => Error.Conflict(
            "TransferPolicies.CrossGovernorateBlocked",
            "Transfers between different governorates are blocked by policy.",
            new
            {
                source_warehouse_id = sourceWarehouseId,
                destination_warehouse_id = destinationWarehouseId,
                source_governorate_code = sourceGovernorateCode,
                destination_governorate_code = destinationGovernorateCode
            });

    public static Error GovernorateRequired(Guid sourceWarehouseId, Guid destinationWarehouseId) => Error.Problem(
        "TransferPolicies.GovernorateRequired",
        "Both warehouse sites must have a governorate code before the transfer policy can be evaluated.",
        new
        {
            source_warehouse_id = sourceWarehouseId,
            destination_warehouse_id = destinationWarehouseId
        });
}
