namespace Application.WarehouseCapabilities.GetByWarehouse;

public sealed class WarehouseCapabilityResponse
{
    public Guid Id { get; init; }

    public Guid WarehouseId { get; init; }

    public Guid MaterialDomainId { get; init; }

    public string MaterialDomainCode { get; init; }

    public string MaterialDomainName { get; init; }

    public string Status { get; init; }

    /// <summary>
    /// Every operation currently granted to this capability, in the
    /// <c>Domain.Common.OperationType</c> declaration order: Receiving, Issue, Transfer,
    /// Count, Return, Adjustment.
    /// </summary>
    /// <remarks>
    /// Read model for the capability matrix. The frontend edits a matrix, and RESOLUTION-020
    /// (<c>target-contract-baseline.md:369</c>) specifies the read as "Complete Domain/operation
    /// policy plus Warehouse rowVersion". Operations are a child resource and no endpoint joins
    /// them into this projection, so every consumer had to issue 1+N calls to assemble one
    /// screen. Emitting them inline makes the read correct under both candidate write models, so
    /// it does not prejudge the open RESOLUTION-020 question.
    ///
    /// Typed as string rather than as OperationType. This response is emitted through
    /// Results.Ok, which serialises with the minimal-API JsonOptions rather than the MVC options
    /// the global JsonStringEnumConverter is registered on, so an enum-typed property is written
    /// as its numeric ordinal unless it carries an explicit converter attribute. That escape
    /// hatch does not extend to a collection: the attribute would have to be
    /// JsonConverter&lt;IReadOnlyList&lt;OperationType&gt;&gt; rather than
    /// JsonConverter&lt;OperationType&gt;. Every enum collection in this API is therefore typed as
    /// string, including the sibling WarehouseCapabilityOperationResponse.OperationType. The
    /// DTO here stays consistent with that convention rather than introducing a bespoke
    /// collection converter for one field.
    ///
    /// Settable rather than init-only because the handler pages the capability rows first and
    /// then attaches operations in a second query; EF Core cannot project a collection across a
    /// paged Skip/Take. This mirrors the two-phase assembly already used by
    /// GetWarehouseDocumentByIdQueryHandler for WarehouseDocumentDetailsResponse.Lines. The
    /// default is an empty list so a capability with no operation rows serialises as [] and never
    /// as null, because clients bind this array directly.
    /// </remarks>
    public IReadOnlyList<string> Operations { get; set; } = [];
}