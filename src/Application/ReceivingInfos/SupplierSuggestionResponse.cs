namespace Application.ReceivingInfos;

/// <summary>Minimal data needed to select an active external supplier.</summary>
public sealed record SupplierSuggestionResponse(Guid Id, string NameAr, string? Code);
