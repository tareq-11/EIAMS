using Application.Abstractions.PolymorphicReferences;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace Infrastructure.PolymorphicReferences;

internal sealed class PolymorphicReferenceAuditor(
    ApplicationDbContext dbContext,
    IOptions<PolymorphicReferenceAuditOptions> options) : IPolymorphicReferenceAuditor
{
    public async Task<PolymorphicReferenceAuditResult> AuditAsync(
        int maximumFindings,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumFindings, 1);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        bool lockAcquired = await dbContext.Database
            .SqlQueryRaw<bool>(
                "SELECT pg_try_advisory_xact_lock({0}) AS \"Value\"",
                options.Value.AdvisoryLockKey)
            .SingleAsync(cancellationToken);

        if (!lockAcquired)
        {
            return new PolymorphicReferenceAuditResult(0, [], true);
        }

        List<PolymorphicReferenceFindingRow> rows = await dbContext.Database
            .SqlQueryRaw<PolymorphicReferenceFindingRow>(AuditSql, maximumFindings)
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        int totalFindings = rows.Count == 0 ? 0 : rows[0].TotalFindings;
        IReadOnlyList<PolymorphicReferenceFinding> findings = rows
            .Select(row => new PolymorphicReferenceFinding(
                row.SourceType,
                row.SourceId,
                row.PartyType,
                row.PartyId,
                row.LifecycleStatus,
                row.Reason))
            .ToList();

        return new PolymorphicReferenceAuditResult(totalFindings, findings, false);
    }

    private const string AuditSql =
        """
        WITH reference_rows AS (
            SELECT 'IssueTo'::text AS source_type,
                   issue.document_id AS source_id,
                   issue.recipient_type AS party_type,
                   issue.recipient_id AS party_id,
                   document.document_status AS lifecycle_status
            FROM public.issue_to AS issue
            INNER JOIN public.warehouse_documents AS document ON document.id = issue.document_id
            UNION ALL
            SELECT 'Custody'::text,
                   custody.id,
                   custody.holder_type,
                   custody.holder_id,
                   custody.status
            FROM public.custodies AS custody
            WHERE custody.status = 'Active'
        ), evaluated AS (
            SELECT reference.*,
                   CASE
                       WHEN reference.party_type = 'External' THEN 'UnsupportedType'
                       WHEN reference.party_type = 'Employee' AND employee.id IS NULL THEN 'NotFound'
                       WHEN reference.party_type = 'Employee' AND employee.status <> 'Active' THEN 'Inactive'
                       WHEN reference.party_type = 'OrganizationalUnit' AND organizational_unit.id IS NULL THEN 'NotFound'
                       WHEN reference.party_type = 'OrganizationalUnit' AND organizational_unit.status <> 'Active' THEN 'Inactive'
                       WHEN reference.party_type = 'Site' AND site.id IS NULL THEN 'NotFound'
                       WHEN reference.party_type = 'Site' AND site.status <> 'Active' THEN 'Inactive'
                       WHEN reference.party_type NOT IN ('Employee', 'OrganizationalUnit', 'Site', 'External') THEN 'UnsupportedType'
                   END AS reason
            FROM reference_rows AS reference
            LEFT JOIN public.employees AS employee
                ON reference.party_type = 'Employee' AND employee.id = reference.party_id
            LEFT JOIN public.organizational_units AS organizational_unit
                ON reference.party_type = 'OrganizationalUnit' AND organizational_unit.id = reference.party_id
            LEFT JOIN public.sites AS site
                ON reference.party_type = 'Site' AND site.id = reference.party_id
        ), findings AS (
            SELECT * FROM evaluated WHERE reason IS NOT NULL
        )
        SELECT source_type,
               source_id,
               party_type,
               party_id,
               lifecycle_status,
               reason,
               count(*) OVER ()::integer AS total_findings
        FROM findings
        ORDER BY source_type, source_id
        LIMIT {0}
        """;

    private sealed class PolymorphicReferenceFindingRow
    {
#pragma warning disable S3459, S1144 // EF materializes these members from raw SQL.
        public string SourceType { get; set; } = string.Empty;

        public Guid SourceId { get; set; } = Guid.Empty;

        public string PartyType { get; set; } = string.Empty;

        public Guid PartyId { get; set; } = Guid.Empty;

        public string LifecycleStatus { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;

        public int TotalFindings { get; set; }
#pragma warning restore S3459, S1144
    }
}
