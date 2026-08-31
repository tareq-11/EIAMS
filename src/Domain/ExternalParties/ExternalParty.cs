using Domain.Common;
using SharedKernel;

namespace Domain.ExternalParties;

public sealed class ExternalParty : Entity, IAuditableEntity
{
    public const int MaxNameLength = 200;
    public const int MaxCodeLength = 50;
    public const int MaxContactInfoLength = 500;
    public const int MaxNotesLength = 1000;

    private ExternalParty() { }

    public string NameAr { get; private set; }
    public string NormalizedNameAr { get; private set; }
    public string? Code { get; private set; }
    public string? NormalizedCode { get; private set; }
    public string? ContactInfo { get; private set; }
    public string? Notes { get; private set; }
    public Status Status { get; private set; }
    public int RowVersion { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static ExternalParty Create(
        Guid id,
        string nameAr,
        string? code,
        string? contactInfo,
        string? notes)
    {
        var party = new ExternalParty
        {
            Id = id,
            NameAr = NormalizeRequired(nameAr),
            NormalizedNameAr = NormalizeKey(nameAr),
            Code = NormalizeOptional(code),
            NormalizedCode = NormalizeOptionalKey(code),
            ContactInfo = NormalizeOptional(contactInfo),
            Notes = NormalizeOptional(notes),
            Status = Status.Active,
            RowVersion = 1
        };

        party.Raise(new ExternalPartyCreatedDomainEvent(party.Id));
        return party;
    }

    public void UpdateDetails(string nameAr, string? code, string? contactInfo, string? notes)
    {
        string normalizedName = NormalizeRequired(nameAr);
        string normalizedNameKey = NormalizeKey(nameAr);
        string? normalizedCode = NormalizeOptional(code);
        string? normalizedCodeKey = NormalizeOptionalKey(code);
        string? normalizedContact = NormalizeOptional(contactInfo);
        string? normalizedNotes = NormalizeOptional(notes);

        if (NameAr == normalizedName &&
            NormalizedNameAr == normalizedNameKey &&
            Code == normalizedCode &&
            NormalizedCode == normalizedCodeKey &&
            ContactInfo == normalizedContact &&
            Notes == normalizedNotes)
        {
            return;
        }

        NameAr = normalizedName;
        NormalizedNameAr = normalizedNameKey;
        Code = normalizedCode;
        NormalizedCode = normalizedCodeKey;
        ContactInfo = normalizedContact;
        Notes = normalizedNotes;
        RowVersion++;
        Raise(new ExternalPartyUpdatedDomainEvent(Id));
    }

    public void SetStatus(Status status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;
        RowVersion++;
        Raise(new ExternalPartyStatusChangedDomainEvent(Id, status));
    }

    private static string NormalizeRequired(string value) => value.Trim();
    private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();
    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeOptionalKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
