-- Read-only rollout report. Run against the target database before deploying 4C.
-- Legacy free-form SupplierRef values are intentionally not mapped automatically.
-- Any rows with no supplier_party_id require an explicit business-owned mapping.

SELECT
    count(*) AS total_receiving_rows,
    count(*) FILTER (WHERE supplier_party_id IS NOT NULL) AS linked_rows,
    count(*) FILTER (WHERE supplier_party_id IS NULL) AS legacy_unlinked_rows
FROM public.receiving_info;

SELECT
    ri.document_id,
    wd.system_reference_number,
    wd.paper_document_number,
    wd.document_status,
    ri.supplier_ref,
    ri.supplier_party_id,
    CASE
        WHEN ri.supplier_party_id IS NOT NULL THEN 'Linked'
        WHEN EXISTS (
            SELECT 1 FROM public.employees e
            WHERE lower(btrim(e.full_name)) = lower(btrim(ri.supplier_ref))
               OR lower(btrim(e.employee_number)) = lower(btrim(ri.supplier_ref))) THEN 'MatchesInternalEmployeeNameOrNumber'
        WHEN EXISTS (
            SELECT 1 FROM public.sites s
            WHERE lower(btrim(s.name)) = lower(btrim(ri.supplier_ref))
               OR lower(btrim(s.code)) = lower(btrim(ri.supplier_ref))) THEN 'MatchesInternalSiteNameOrCode'
        WHEN EXISTS (
            SELECT 1 FROM public.organizational_units ou
            WHERE lower(btrim(ou.name)) = lower(btrim(ri.supplier_ref))) THEN 'MatchesInternalOrgUnitName'
        WHEN EXISTS (
            SELECT 1 FROM public.external_parties ep
            WHERE lower(btrim(ep.name_ar)) = lower(btrim(ri.supplier_ref))
               OR lower(btrim(ep.code)) = lower(btrim(ri.supplier_ref))) THEN 'PossibleExternalPartyMatchNeedsReview'
        ELSE 'UnresolvedLegacyReference'
    END AS preflight_classification
FROM public.receiving_info ri
JOIN public.warehouse_documents wd ON wd.id = ri.document_id
WHERE ri.supplier_party_id IS NULL
ORDER BY ri.document_id;

-- Deployment gate: review all rows above. Do not infer/auto-link by text. New API writes
-- are safe because they require an active ExternalParty ID and persist its FK.
