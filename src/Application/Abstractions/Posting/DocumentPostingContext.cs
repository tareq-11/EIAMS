using Domain.DocumentLines;
using Domain.WarehouseDocuments;
using Domain.Warehouses;

namespace Application.Abstractions.Posting;

/// <summary>Everything a posting strategy needs, gathered once by the coordinator.</summary>
public sealed record DocumentPostingContext(
    WarehouseDocument Document,
    Warehouse Warehouse,
    IReadOnlyList<DocumentLine> Lines,
    Guid PostedBy,
    DateTime PostedAtUtc);
