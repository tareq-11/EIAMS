Phase 4C external supplier-reference preflight

Run phase4c-supplier-reference-preflight.sql read-only against the intended database
before deployment. It reports every ReceivingInfo without an ExternalParty link and
labels exact text matches to internal employee/site/org-unit names for human review.

No legacy SupplierRef is automatically assigned to an ExternalParty. A name or code
match is not proof of identity. Resolve rows using authoritative supplier records and
reviewed mappings; keep historic supplier_ref text unchanged. Existing posted
documents can remain unlinked for historical display, but legacy drafts requiring
further mutation should be explicitly relinked through the API before posting.

Phase 4C writes are constrained by a nullable FK to external_parties and the API
requires supplierPartyId for every new/upserted ReceivingInfo. Inactive/nonexistent
supplier IDs are rejected. Transfers and returns continue using their independent
TransferInfo and ReturnInfo routes; ReceivingInfo cannot represent those types.
