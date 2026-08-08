using System.Globalization;
using Application.Abstractions.Numbering;
using Domain.Common;
using Domain.DocumentSequences;
using Domain.Sites;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.Numbering;

internal sealed class ReferenceNumberGenerator(
    ApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IOptions<NumberingOptions> numberingOptions)
    : IReferenceNumberGenerator
{
    private readonly NumberingOptions options = numberingOptions.Value;

    public async Task<Result<string>> AllocateAsync(
        Guid siteId,
        DocumentType documentType,
        CancellationToken cancellationToken)
    {
        Site? site = await dbContext.Sites.SingleOrDefaultAsync(s => s.Id == siteId, cancellationToken);

        if (site is null)
        {
            return Result.Failure<string>(DocumentSequenceErrors.SiteNotFound(siteId));
        }

        if (site.Status != Status.Active)
        {
            return Result.Failure<string>(DocumentSequenceErrors.SiteInactive(siteId));
        }

        if (!Enum.IsDefined(documentType))
        {
            return Result.Failure<string>(DocumentSequenceErrors.InvalidDocumentType(documentType));
        }

        if (site.Code.Contains(options.Separator, StringComparison.Ordinal))
        {
            return Result.Failure<string>(
                DocumentSequenceErrors.SiteCodeContainsSeparator(site.Code, options.Separator));
        }

        DateTime utcNow = dateTimeProvider.UtcNow;
        int year = utcNow.Year;
        string documentTypeCode = options.DocumentTypeCode(documentType);
        string minimumReferenceNumber = string.Join(
            options.Separator,
            site.Code,
            documentTypeCode,
            year.ToString(CultureInfo.InvariantCulture),
            new string('0', options.SequencePadding));

        if (minimumReferenceNumber.Length > options.MaxReferenceNumberLength)
        {
            return Result.Failure<string>(
                DocumentSequenceErrors.ReferenceNumberTooLong(options.MaxReferenceNumberLength));
        }

        int lastSequence = await AllocateSequenceAsync(siteId, documentType, year, utcNow, cancellationToken);

        string sequence = lastSequence.ToString($"D{options.SequencePadding}", CultureInfo.InvariantCulture);
        string referenceNumber = string.Join(
            options.Separator,
            site.Code,
            documentTypeCode,
            year.ToString(CultureInfo.InvariantCulture),
            sequence);

        if (referenceNumber.Length > options.MaxReferenceNumberLength)
        {
            return Result.Failure<string>(
                DocumentSequenceErrors.ReferenceNumberTooLong(options.MaxReferenceNumberLength));
        }

        return referenceNumber;
    }

    private async Task<int> AllocateSequenceAsync(
        Guid siteId,
        DocumentType documentType,
        int year,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        // Atomic upsert: ON CONFLICT ... DO UPDATE is a single row-locking statement in Postgres, so
        // concurrent callers for the same (site, document type, year) key never receive the same
        // sequence number. This intentionally bypasses EF change tracking - DocumentSequence has no
        // public mutation methods (see Domain.DocumentSequences.DocumentSequence).
        List<int> results = await dbContext.Database.SqlQueryRaw<int>(
            """
            INSERT INTO document_sequences
                (id, site_id, document_type, year, last_sequence, created_at_utc, created_by, updated_at_utc, updated_by)
            VALUES
                ({0}, {1}, {2}, {3}, 1, {4}, NULL, NULL, NULL)
            ON CONFLICT (site_id, document_type, year)
            DO UPDATE SET
                last_sequence = document_sequences.last_sequence + 1,
                updated_at_utc = {4},
                updated_by = NULL
            RETURNING last_sequence;
            """,
            Guid.NewGuid(),
            siteId,
            documentType.ToString(),
            year,
            utcNow).ToListAsync(cancellationToken);

        return results[0];
    }
}
