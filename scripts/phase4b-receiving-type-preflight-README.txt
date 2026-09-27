Phase 4B ReceivingType preflight

Run `psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f scripts/phase4b-receiving-type-preflight.sql` against the target database before deploying the Supplier-only API/migration. The script is read-only and reports grouped counts, the document/detail evidence for each row, and a classification.

The migration retains valid Supplier rows. It archives a legacy Transfer/Return row only when the same document already has the corresponding `TransferInfo`/`ReturnInfo`, has the matching document type, and has no competing detail. The original supplier-reference and invoice values, row audit columns, type, and document ID remain in `public.receiving_info_legacy_classifications` for traceability and future reviewed remediation.

Any other non-Supplier row is ambiguous and makes the migration fail without changing the row or constraint. In particular, `supplier_ref` and `supplier_invoice_ref` are free-form text; they are not enough to infer a destination warehouse, original issue document, or transfer/return reason. Review and remediate those rows through a separately approved, evidence-based data migration, then rerun the preflight. Do not disable triggers or infer IDs from those strings on production.

Do not deploy this cutover if the result contains `AMBIGUOUS_BLOCKS_MIGRATION`. After deployment, verify that only Supplier remains in `receiving_info` and that the migration's Supplier-only constraint is present. The migration's `Down` intentionally refuses to discard archived classification rows; restore from a reviewed backup or forward-fix instead.
