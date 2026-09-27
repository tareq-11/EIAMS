-- Read-only report before the site/year sequence migration.
-- The migration keeps max(last_sequence) per (site, year); it does not rewrite issued documents.

SELECT site_id, year, count(*) AS old_counter_rows, max(last_sequence) AS retained_high_watermark
FROM public.document_sequences
GROUP BY site_id, year
HAVING count(*) > 1
ORDER BY site_id, year;

SELECT site_id, year, document_type, last_sequence
FROM public.document_sequences
ORDER BY site_id, year, document_type;

SELECT count(*) AS documents_with_system_reference
FROM public.warehouse_documents
WHERE system_reference_number IS NOT NULL;

-- Compare system_reference_number snapshots before/after rollout in the target environment.
-- Rollback cannot reconstruct each old per-type counter; use a reviewed backup or forward-fix.
