-- Read-only preflight for Phase 5A. Run against a disposable/staging database before migration.
-- Any active/terminal counts with no line snapshot must be investigated; the migration fails closed.

SELECT
    c.id,
    c.warehouse_id,
    c.status,
    c.planned_at_utc,
    c.started_at_utc
FROM public.inventory_counts AS c
WHERE c.status IN ('InProgress', 'Completed', 'Closed')
  AND NOT EXISTS (
      SELECT 1
      FROM public.inventory_count_lines AS line
      WHERE line.count_id = c.id
  )
ORDER BY c.warehouse_id, c.planned_at_utc, c.id;

-- Planned counts with pre-existing snapshots are not guessed/backfilled: Start refreshes their
-- snapshots from the current warehouse state while still Planned, then locks membership.
SELECT
    c.status,
    COUNT(*) AS count_total,
    COUNT(*) FILTER (WHERE line_summary.line_count > 0) AS counts_with_prestart_lines
FROM public.inventory_counts AS c
LEFT JOIN LATERAL (
    SELECT COUNT(*) AS line_count
    FROM public.inventory_count_lines AS line
    WHERE line.count_id = c.id
) AS line_summary ON TRUE
GROUP BY c.status
ORDER BY c.status;
