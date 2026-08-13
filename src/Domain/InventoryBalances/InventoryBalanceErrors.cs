using SharedKernel;

namespace Domain.InventoryBalances;

public static class InventoryBalanceErrors
{
    public static Error NegativeQuantity(Guid warehouseId, Guid materialId, decimal quantity) => Error.Problem(
        "InventoryBalances.NegativeQuantity",
        "InventoryBalance quantity cannot become negative.",
        new { warehouse_id = warehouseId, material_id = materialId, quantity });

    public static Error InsufficientQuantity(Guid warehouseId, Guid materialId, decimal available, decimal requested) =>
        Error.Problem(
            "InventoryBalances.InsufficientQuantity",
            "The requested quantity exceeds the available balance.",
            new
            {
                warehouse_id = warehouseId,
                material_id = materialId,
                available_quantity = available,
                requested_quantity = requested
            });
}
