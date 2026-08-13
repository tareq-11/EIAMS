using Domain.DocumentLines;
using Domain.Warehouses;
using Domain.WarehouseDocuments;

namespace Application.Abstractions.Posting;

/// <summary>Everything a posting strategy needs, gathered once by the coordinator.</summary>
public sealed record DocumentPostingContext(
    WarehouseDocument Document,
    Warehouse Warehouse,
    IReadOnlyList<DocumentLine> Lines,
    Guid PostedBy,
    DateTime PostedAtUtc);
