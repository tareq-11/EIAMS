using Domain.Common;

namespace Application.Abstractions.Numbering;

/// <summary>
/// Configurable formatting policy for system reference numbers (D-SEQ-01). The PRD only constrains
/// the underlying <c>system_reference_number</c> column (VARCHAR(100) UNIQUE), so this is the single
/// place that owns and validates the format:
/// {SiteCode}-{DocumentTypeCode}-{Year}-{Sequence:D6}, e.g. HQ-RCV-2026-000042.
/// </summary>
public sealed class NumberingOptions
{
    public const string SectionName = "Numbering";

    public int SequencePadding { get; init; } = 6;

    public string Separator { get; init; } = "-";

    public int MaxReferenceNumberLength { get; init; } = 100;

    public string ReceivingCode { get; init; } = "RCV";

    public string IssueCode { get; init; } = "ISS";

    public string TransferCode { get; init; } = "TRF";

    public string AdjustmentCode { get; init; } = "ADJ";

    public string OpeningCode { get; init; } = "OPN";

    public string ReturnCode { get; init; } = "RET";

    public string DocumentTypeCode(DocumentType documentType) => documentType switch
    {
        DocumentType.Receiving => ReceivingCode,
        DocumentType.Issue => IssueCode,
        DocumentType.Transfer => TransferCode,
        DocumentType.Adjustment => AdjustmentCode,
        DocumentType.Opening => OpeningCode,
        DocumentType.Return => ReturnCode,
        _ => throw new ArgumentOutOfRangeException(nameof(documentType), documentType, "Unknown document type.")
    };

    public IEnumerable<string> DocumentTypeCodes()
    {
        yield return ReceivingCode;
        yield return IssueCode;
        yield return TransferCode;
        yield return AdjustmentCode;
        yield return OpeningCode;
        yield return ReturnCode;
    }
}
