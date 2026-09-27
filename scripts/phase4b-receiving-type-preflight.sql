-- Phase 4B read-only inventory. Run against the intended database before deployment.
-- This script does not change data or schema. Review every non-Supplier row before continuing.

-- 1) Counts by legacy type and document state/type.
SELECT
    COALESCE(ri.receiving_type, '<NULL>') AS receiving_type,
    COALESCE(wd.document_type, '<ORPHAN>') AS document_type,
    COALESCE(wd.document_status, '<ORPHAN>') AS document_status,
    COUNT(*) AS rows
FROM public.receiving_info AS ri
LEFT JOIN public.warehouse_documents AS wd ON wd.id = ri.document_id
GROUP BY ri.receiving_type, wd.document_type, wd.document_status
ORDER BY ri.receiving_type NULLS FIRST, wd.document_type NULLS FIRST, wd.document_status NULLS FIRST;

-- 2) Per-row classification. Only Transfer/Return rows that already have their matching
--    document type and typed detail are unambiguously represented elsewhere and will be archived
--    by the migration. Every other non-Supplier value blocks the migration for manual remediation.
SELECT
    ri.document_id,
    ri.receiving_type,
    wd.document_type,
    wd.document_status,
    ri.supplier_ref AS legacy_supplier_ref,
    ri.supplier_invoice_ref AS legacy_supplier_invoice_ref,
    (ti.document_id IS NOT NULL) AS has_transfer_info,
    ti.destination_warehouse_id,
    (ret.document_id IS NOT NULL) AS has_return_info,
    ret.original_issue_document_id,
    CASE
        WHEN ri.receiving_type = 'Supplier'
         AND wd.document_type = 'Receiving'
         AND ti.document_id IS NULL
         AND ret.document_id IS NULL
            THEN 'KEEP_SUPPLIER'
        WHEN ri.receiving_type = 'Transfer'
         AND wd.document_type = 'Transfer'
         AND ti.document_id IS NOT NULL
         AND ret.document_id IS NULL
            THEN 'ARCHIVE_DUPLICATE_TRANSFER_DETAIL'
        WHEN ri.receiving_type = 'Return'
         AND wd.document_type = 'Return'
         AND ret.document_id IS NOT NULL
         AND ti.document_id IS NULL
            THEN 'ARCHIVE_DUPLICATE_RETURN_DETAIL'
        ELSE 'AMBIGUOUS_BLOCKS_MIGRATION'
    END AS migration_classification
FROM public.receiving_info AS ri
LEFT JOIN public.warehouse_documents AS wd ON wd.id = ri.document_id
LEFT JOIN public.transfer_info AS ti ON ti.document_id = ri.document_id
LEFT JOIN public.return_info AS ret ON ret.document_id = ri.document_id
ORDER BY migration_classification, ri.document_id;

-- 3) Summary of rows that the migration will refuse to guess about.
WITH classified AS (
    SELECT
        ri.document_id,
        CASE
            WHEN ri.receiving_type = 'Supplier'
             AND wd.document_type = 'Receiving'
             AND ti.document_id IS NULL
             AND ret.document_id IS NULL THEN 'KEEP_SUPPLIER'
            WHEN ri.receiving_type = 'Transfer'
             AND wd.document_type = 'Transfer'
             AND ti.document_id IS NOT NULL
             AND ret.document_id IS NULL THEN 'ARCHIVE_DUPLICATE_TRANSFER_DETAIL'
            WHEN ri.receiving_type = 'Return'
             AND wd.document_type = 'Return'
             AND ret.document_id IS NOT NULL
             AND ti.document_id IS NULL THEN 'ARCHIVE_DUPLICATE_RETURN_DETAIL'
            ELSE 'AMBIGUOUS_BLOCKS_MIGRATION'
        END AS migration_classification
    FROM public.receiving_info AS ri
    LEFT JOIN public.warehouse_documents AS wd ON wd.id = ri.document_id
    LEFT JOIN public.transfer_info AS ti ON ti.document_id = ri.document_id
    LEFT JOIN public.return_info AS ret ON ret.document_id = ri.document_id
)
SELECT migration_classification, COUNT(*) AS rows
FROM classified
GROUP BY migration_classification
ORDER BY migration_classification;
