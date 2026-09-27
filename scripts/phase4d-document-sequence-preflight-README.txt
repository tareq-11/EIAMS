Phase 4D site/year document-number preflight

Run the SQL read-only report against the target database before deployment. The new allocator has
one atomic counter per SiteId+year across all document types; document-type prefixes in existing
and newly issued system references remain unchanged. Existing per-type counters are consolidated
to their maximum high-water mark, so the next number is greater than all old per-type counters and
is never reused. Existing WarehouseDocument.system_reference_number values are not rewritten.

A follow-up additive migration stores a nullable `(reference_site_id, reference_year,
reference_sequence)` identity on newly created drafts and adds a partial unique index over that
tuple. The three fields must be all-null (legacy rows) or all-present (new allocated rows), and
new drafts created through the application populate the tuple from the atomic allocator. Existing
system reference strings are not parsed or backfilled. Rollback of this identity migration is
blocked once any tuple has been allocated, to prevent silent loss of the uniqueness evidence.

Review unusual/duplicate old counters and preserve the report with the release evidence. Migration
rollback is intentionally blocked because the consolidated counter cannot be split back into exact
historical per-type values without a backup. Use a reviewed restore or a forward fix; do not guess.
