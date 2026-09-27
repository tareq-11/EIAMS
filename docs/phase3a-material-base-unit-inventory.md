# Phase 3A — Material base-unit and classification inventory

Status: **inventory complete; 3A gate recorded for review**

This is a read-only blast-radius inventory for decision 038. It deliberately does
not remove `MaterialFamily.BaseUnitId`, change a migration, or alter runtime
behaviour. Those changes belong to 3C, after the preflight gate described in the
implementation plan passes.

## Decision boundary

`Material.BaseUnitId` is the only intended source of truth for a material's base
unit. `MaterialFamily.BaseUnitId` is still present in the current model and is
classified below as a legacy source to remove in 3C. A family remains the
classification hierarchy (`Domain -> Category -> Family -> Material`); it does
not own material quantity semantics.

## Current production surface

### Authoritative material source (must remain)

- `src/Domain/Materials/Material.cs` — `BaseUnitId` domain property and factory input.
- `src/Infrastructure/Materials/MaterialConfiguration.cs` — material-to-unit FK.
- `src/Application/Materials/Create/CreateMaterialCommand.cs`
- `src/Application/Materials/Create/CreateMaterialCommandHandler.cs`
- `src/Application/Materials/Create/CreateMaterialCommandValidator.cs`
- `src/Application/Materials/GetById/GetMaterialByIdQueryHandler.cs`
- `src/Application/Materials/GetById/MaterialResponse.cs`
- `src/Application/Materials/GetList/GetMaterialsQueryHandler.cs`
- `src/Application/Materials/GetList/MaterialResponse.cs`
- `src/Web.Api/Controllers/Materials/CreateMaterialController.cs`

These are the files that must supply/return `baseUnitId` after 3C. No family
value may be used to fill it.

### Legacy family source (3C removal scope)

- `src/Domain/MaterialFamilies/MaterialFamily.cs`
- `src/Domain/MaterialFamilies/MaterialFamilyErrors.cs`
- `src/Infrastructure/MaterialFamilies/MaterialFamilyConfiguration.cs`
- `src/Application/MaterialFamilies/Create/CreateMaterialFamilyCommand.cs`
- `src/Application/MaterialFamilies/Create/CreateMaterialFamilyCommandHandler.cs`
- `src/Application/MaterialFamilies/Create/CreateMaterialFamilyCommandValidator.cs`
- `src/Application/MaterialFamilies/GetById/GetMaterialFamilyByIdQueryHandler.cs`
- `src/Application/MaterialFamilies/GetById/MaterialFamilyResponse.cs`
- `src/Application/MaterialFamilies/GetList/GetMaterialFamiliesQueryHandler.cs`
- `src/Application/MaterialFamilies/GetList/MaterialFamilyResponse.cs`
- `src/Web.Api/Controllers/MaterialFamilies/CreateMaterialFamilyController.cs`
- `src/Web.Api/Controllers/MaterialFamilies/GetMaterialFamiliesController.cs`
- `src/Web.Api/Controllers/MaterialFamilies/GetMaterialFamilyByIdController.cs`

The update/status family commands and controllers are part of the family
contract review, but do not currently accept or mutate a base unit:

- `src/Application/MaterialFamilies/Update/UpdateMaterialFamilyCommand.cs`
- `src/Application/MaterialFamilies/Update/UpdateMaterialFamilyCommandHandler.cs`
- `src/Application/MaterialFamilies/SetStatus/SetMaterialFamilyStatusCommand.cs`
- `src/Application/MaterialFamilies/SetStatus/SetMaterialFamilyStatusCommandHandler.cs`
- `src/Web.Api/Controllers/MaterialFamilies/UpdateMaterialFamilyController.cs`
- `src/Web.Api/Controllers/MaterialFamilies/SetMaterialFamilyStatusController.cs`

### Direct readers of the legacy family base unit (3C removal scope)

Each of these reads `MaterialFamily.BaseUnitId` (or receives it as a parameter)
today. They are blocking consumers for 3C: every one must derive the base unit
from `Material.BaseUnitId` before the family column can be dropped.

- `src/Application/DocumentLines/DocumentLineCatalogResolver.cs` — joins the
  family base unit for base-unit existence, reports `UnitNotFound` for it,
  accepts the requested unit only when it equals it, and requires the resolved
  conversion target to match it. `DocumentLineCatalogContext.Family` is the only
  carrier of the value, so this resolver is the first thing 3C must change.
- `src/Application/DocumentLines/BaseQuantityCalculator.cs` — pure calculation
  with no persistence access; it takes a `familyBaseUnitId` parameter that must
  become material-derived at every call site.
- `src/Application/DocumentLines/DocumentLineSubmissionValidator.cs` — collects
  `row.Family.BaseUnitId` for unit-existence checks, requires the conversion
  target to equal it, and passes it to `BaseQuantityCalculator.Calculate`.
- `src/Application/DocumentLines/Add/AddDocumentLineCommandHandler.cs` — passes
  `catalog.Family.BaseUnitId` into `BaseQuantityCalculator.Calculate`.
- `src/Application/DocumentLines/Update/UpdateDocumentLineCommandHandler.cs` —
  same call shape as add.
- `src/Application/InventoryAdjustments/AddLine/AddAdjustmentLineCommandHandler.cs`
  — passes `catalog.Family.BaseUnitId` (it also branches on
  `catalog.Material.IsAssetTracked`, which is a separate classification read).
- `src/Application/InventoryAdjustments/UpdateLine/UpdateAdjustmentLineCommandHandler.cs`
  — passes `catalogResult.Value.Family.BaseUnitId`.
- `src/Application/InventoryAdjustments/CreateDisposal/CreateDisposalCommandHandler.cs`
  — projects `family.BaseUnitId` per material into the disposal line base unit.
- `src/Application/InventoryAdjustments/CreateFromCount/CreateAdjustmentFromCountCommandHandler.cs`
  — projects `family.BaseUnitId` in the variance query and uses it as the
  count-derived adjustment line unit (it also uses kind/`RequiresAssetNumber` for
  asset selection, a separate classification read).

### Related conversion consumers (material-scoped, not family base-unit readers)

These are in the 3B/3C worklist because they constrain base-unit provenance, but
they do **not** read the family value and are therefore not 3C blockers:

- `src/Application/MaterialUnitConversions/Add/AddMaterialUnitConversionCommandHandler.cs`
  — reads `Material.BaseUnitId` and already requires the conversion target to
  equal it. Retain this rule; it becomes the reference rule once the family
  column is gone.
- `src/Application/MaterialUnitConversions/GetByMaterial/GetMaterialUnitConversionsQueryHandler.cs`
  — projects `MaterialUnitConversion.ToBaseUnitId`.
- `src/Application/MaterialUnitConversions/GetByMaterial/MaterialUnitConversionResponse.cs`
  — exposes the conversion's `ToBaseUnitId` response field.
- `src/Application/MaterialUnitConversions/GetById/GetMaterialUnitConversionByIdQueryHandler.cs`
  — projects `ToBaseUnitId`.

`ToBaseUnitId` is the conversion row's own target-unit column
(`src/Domain/MaterialUnitConversions/MaterialUnitConversion.cs`), not a family
value. 3B/3C still has to decide whether that target stays pinned to the
material base unit, so these stay in the worklist — but listing them as family
base-unit readers would be inaccurate.

`src/Infrastructure/WarehouseDocuments/PostingMaterialCatalogLoader.cs` also
belongs to the worklist, but it is a classification/validation consumer rather
than a base-unit reader: it loads the family to assert the family exists, is
active, and resolves its category, and it derives the document line type from
`Material.IsAssetTracked`. It reads no `BaseUnitId`, so it is not a 3C blocker;
its `IsAssetTracked` read is catalogued in the classification inventory below.

### Persistence and historical migration surface

- `src/Infrastructure/Database/ApplicationDbContext.cs` and
  `src/Application/Abstractions/Data/IApplicationDbContext.cs` expose both
  DbSets and must remain internally consistent during the cutover.
- `src/Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs` is the
  only model snapshot to update in 3C.
- Historical migration/designer files containing the old family column are
  immutable evidence and must not be edited. The relevant history includes
  `20260804224839_Add_MaterialCatalog.*` and later designer snapshots, plus
  `20260828210000_AddMaterialBaseUnit.*`.
- `src/Infrastructure/MaterialUnitConversions/MaterialUnitConversionConfiguration.cs`
  is a separate material-scoped conversion table; it is not a family base-unit
  relationship.

## API and contract surface

Current family create/read DTOs/controllers still expose or accept `baseUnitId`:

- `src/Web.Api/Controllers/MaterialFamilies/CreateMaterialFamilyController.cs`
- `src/Application/MaterialFamilies/Create/CreateMaterialFamilyCommand.cs`
- `src/Application/MaterialFamilies/GetById/MaterialFamilyResponse.cs`
- `src/Application/MaterialFamilies/GetList/MaterialFamilyResponse.cs`
- `contracts/openapi/eiams-backend-v1.openapi.json`

The 3C contract target is family create/read without `baseUnitId`, while material
create/read continues to expose `baseUnitId`. The envelope and top-level
pagination are unrelated and must remain unchanged.

## Test and fixture blast radius

All current `MaterialFamily.Create(..., baseUnitId)` fixtures must be updated in
3C only after the production model change:

- `tests/Application.UnitTests/M1/MaterialCatalogHandlerTests.cs`
- `tests/Application.UnitTests/M7/InventoryAdjustmentMutationTests.cs`
- `tests/Application.UnitTests/M7/PlanInventoryCountCommandHandlerTests.cs`
- `tests/Application.UnitTests/MaterialUnitConversions/MaterialUnitConversionHandlerTests.cs`
- `tests/IntegrationTests/M0M1/M0M1AuthorizationAndDatabaseTests.cs`
- `tests/IntegrationTests/M4/M4PostingTests.cs`
- `tests/IntegrationTests/M4/DraftCreationAtomicityTests.cs` — exercises the
  fail-closed document-line creation contract with catalog classification data.
- `tests/IntegrationTests/M4/ReversalIdempotencyApiTests.cs` — seeds classified
  material provenance for the real PostgreSQL reversal/idempotency path.
- `tests/IntegrationTests/M5/M5IssueTests.cs`
- `tests/IntegrationTests/M5/M5TransferAuthorizationTests.cs`
- `tests/IntegrationTests/M5/M5TransferPostingTests.cs`
- `tests/IntegrationTests/M6/M6AssetLifecyclePostingTests.cs`
- `tests/IntegrationTests/M6/M6DatabaseAndApiTests.cs`
- `tests/IntegrationTests/M7/M7AdjustmentAndFreezeTests.cs`
- `tests/IntegrationTests/Phase8/DurableCustodyIntegrationTests.cs`
- `tests/IntegrationTests/Phase10/InventoryReadApiIntegrationTests.cs`
- `tests/IntegrationTests/Performance/SyntheticDatasetSeeder.cs`
- `tests/IntegrationTests/Performance/TrigramSearchIndexTests.cs`
- `tests/IntegrationTests/Regression/RegressionTestHelper.cs`
- `tests/IntegrationTests/Infrastructure/MaterialFamilyBaseUnitMigrationTests.cs` (3C migration gate fixture seeds a material classification)

## 3A gate

3A is complete only when all of the following remain true:

1. This inventory names every current production family/base-unit source,
   downstream consumer, public DTO/controller, snapshot, and fixture.
2. `Material.BaseUnitId` and its FK remain present.
3. No migration file is modified and no migration drops the family column.
4. Every file that reads `MaterialFamily.BaseUnitId` is named in the direct
   readers list above and carried into the 3B/3C worklist; no file is
   misclassified as a family base-unit reader because it merely reads
   `Material.BaseUnitId`, a conversion `ToBaseUnitId`, or classification state.
5. Every `IsAssetTracked`/`MaterialKind`/`TrackingType`/`RequiresAssetNumber`
   reader stays named exactly once in the classification inventory, including
   `PostingMaterialCatalogLoader`.
6. The architecture inventory test and
   `scripts/validate-phase3a-material-inventory.sh` pass.

3B may now design conversion/classification provenance changes. 3C may remove
the family property only after its SQL preflight proves every material has a
valid active base unit and no production consumer still depends on the family
value.

## Classification inventory (decision 032; planning input to 3B, not 3A behavior change)

The classification vocabulary and current persistence/domain representation are:

- `src/Domain/Materials/MaterialKind.cs` (`Consumable`, `Durable`, `Asset`)
- `src/Domain/Materials/TrackingType.cs` (`Quantity`, `Serial`)
- `src/Domain/Materials/Material.cs` — stores both enums and `RequiresAssetNumber`; `IsAssetTracked` currently returns true for either Asset kind **or** the stored flag. Create/update currently set the flag from `MaterialKind == Asset`; this distinction matters because it is not yet a purely derived getter.
- `src/Infrastructure/Materials/MaterialConfiguration.cs` — persists the enum values as strings and configures the material row.

### Write path and public material API

Both write paths accept client-provided enum values and validate the supported
enum/matrix in both validator and handler; `RequiresAssetNumber` is not part of
these commands and is assigned by the domain model:

- Create: `src/Application/Materials/Create/CreateMaterialCommand.cs`,
  `src/Application/Materials/Create/CreateMaterialCommandValidator.cs`,
  `src/Application/Materials/Create/CreateMaterialCommandHandler.cs`,
  `src/Web.Api/Controllers/Materials/CreateMaterialController.cs`.
- Update: `src/Application/Materials/Update/UpdateMaterialCommand.cs`,
  `src/Application/Materials/Update/UpdateMaterialCommandValidator.cs`,
  `src/Application/Materials/Update/UpdateMaterialCommandHandler.cs`,
  `src/Web.Api/Controllers/Materials/UpdateMaterialController.cs`.
- Read projections: `src/Application/Materials/GetById/GetMaterialByIdQueryHandler.cs`,
  `GetById/MaterialResponse.cs`, `GetList/GetMaterialsQueryHandler.cs`,
  `GetList/MaterialResponse.cs`; exposed by
  `src/Web.Api/Controllers/Materials/GetMaterialByIdController.cs` and
  `GetMaterialsController.cs`.
- The other material controllers (`SetMaterialStatusController.cs` and
  `Application/Materials/SetStatus/**`) do not accept or project classification;
  they are included here as a checked negative, not a classification writer.
- `contracts/openapi/eiams-backend-v1.openapi.json` contains both material
  response schemas (`Application.Materials.GetById.MaterialResponse` and
  `...GetList.MaterialResponse`) and their route references. Classification
  enums currently appear as strings in the response schemas; create/update
  request bodies are controller integer enum inputs. `requiresAssetNumber` is
  response-only in those schemas.

### Operational readers of classification / asset tracking

These current source files consume one or more classification properties and
must be included in the 3B policy/provenance review. They are not all unit
conversion consumers:

- `src/Domain/Common/DocumentLineType.cs` — documents that line type is derived
  server-side from material kind.
- `src/Infrastructure/WarehouseDocuments/IssuePostingStrategy.cs` and
  `src/Infrastructure/WarehouseDocuments/ReturnPostingStrategy.cs` — branch on
  kind and tracking type to post
  quantity/serial issue and return effects.
- `src/Application/Custodies/GetCustodies/GetCustodiesQueryHandler.cs` and
  `src/Application/Custodies/GetCustodies/CustodyResponse.cs` — expose material
  kind/tracking type in custody reads.
- `src/Application/Returns/GetEligibleItems/GetReturnEligibleItemsQueryHandler.cs`
  and `src/Application/Returns/GetEligibleItems/ReturnEligibleItemResponse.cs` —
  filter/project kind and tracking type for eligible return items.
- `src/Application/InventoryAdjustments/CreateFromCount/CreateAdjustmentFromCountCommandHandler.cs`
  — uses kind and `RequiresAssetNumber` to choose adjustment/asset selection
  behavior. (It also reads family base unit, separately listed above.)

The following use `Material.IsAssetTracked` (derived: `MaterialKind == Asset || RequiresAssetNumber`)
without directly containing the classification strings. These are caught by the
`IsAssetTracked` grep sweep and must be named here:

- `src/Application/DocumentLines/Add/AddDocumentLineCommandHandler.cs`
- `src/Application/DocumentLines/Update/UpdateDocumentLineCommandHandler.cs`
- `src/Application/DocumentLines/DocumentLineSubmissionValidator.cs`
- `src/Application/InventoryAdjustments/AddLine/AddAdjustmentLineCommandHandler.cs`
- `src/Application/InventoryCounts/Plan/PlanInventoryCountCommandHandler.cs`
- `src/Application/InventoryCounts/ChangeStatus/ChangeInventoryCountStatusCommandHandler.cs`
  — materializes the count's authoritative member snapshot at Start and branches
  on `Material.IsAssetTracked` when snapshotting tracked assets.
- `src/Infrastructure/WarehouseDocuments/PostingMaterialCatalogLoader.cs` — the
  only infrastructure reader; it validates the family (exists, active, category
  resolvable) and then picks the expected document line type from
  `row.Material.IsAssetTracked`. It reads no `BaseUnitId`, so it is a 3B
  classification consumer and is deliberately not repeated in the base-unit
  sections above.

### Classification and tracking test/fixture surface

Tests currently mentioning or exercising material classification/tracking are:

- `tests/Application.UnitTests/M1/MaterialCatalogHandlerTests.cs`
- `tests/Application.UnitTests/M1/MaterialCatalogRulesTests.cs`
- `tests/Application.UnitTests/M4/AssetRulesTests.cs`
- `tests/Application.UnitTests/M7/InventoryAdjustmentMutationTests.cs`
- `tests/Application.UnitTests/M7/PlanInventoryCountCommandHandlerTests.cs`
- `tests/Application.UnitTests/MaterialUnitConversions/MaterialUnitConversionHandlerTests.cs`
- `tests/IntegrationTests/M0M1/M0M1AuthorizationAndDatabaseTests.cs`
- `tests/IntegrationTests/M4/M4PostingTests.cs`
- `tests/IntegrationTests/M5/M5IssueTests.cs`
- `tests/IntegrationTests/M5/M5TransferAuthorizationTests.cs`
- `tests/IntegrationTests/M5/M5TransferPostingTests.cs`
- `tests/IntegrationTests/M6/M6AssetLifecyclePostingTests.cs`
- `tests/IntegrationTests/M6/M6DatabaseAndApiTests.cs`
- `tests/IntegrationTests/M7/M7AdjustmentAndFreezeTests.cs`
- `tests/IntegrationTests/M7/InventoryCountLifecycleApiTests.cs` — seeds a
  classified material and exercises current-state membership materialization.
- `tests/IntegrationTests/Performance/LedgerSetBasedIntegrationTests.cs`
- `tests/IntegrationTests/Performance/SyntheticDatasetSeeder.cs`
- `tests/IntegrationTests/Performance/TrigramSearchIndexTests.cs`
- `tests/IntegrationTests/Performance/WriteScaleBenchmarkTests.cs`
- `tests/IntegrationTests/Phase10/InventoryReadApiIntegrationTests.cs`
- `tests/IntegrationTests/Phase8/DurableCustodyIntegrationTests.cs`
- `tests/IntegrationTests/Regression/RegressionTestHelper.cs`
- `tests/ArchitectureTests/MaterialCatalogInventoryTests.cs` — guards the
  exhaustiveness sweep itself.

The family base-unit fixture list above is intentionally broader than this
classification-specific list: fixture use of `MaterialFamily.Create` alone does
not imply a classification dependency.

### Completeness method and constraints

The validator and architecture test require every current classification source,
write/read DTO path, controller, operational reader, contract, and test listed
above to remain named here. The scoped source sweep is `MaterialKind`,
`TrackingType`, or `RequiresAssetNumber` under `src/{Domain,Application,
Infrastructure,Web.Api}` and `tests`; historical migration designer snapshots
also contain mapped material properties but are immutable generated history,
not operational readers. The active model snapshot is the one listed in the
base-unit section. 3A does not resolve the current classification mismatch or
change endpoint semantics: decision 032's semantic-string request contract,
derived non-writable asset flag, lock-after-operational-use, and server matrix
remain 3B design/implementation scope.

## Phase 3B addendum — conversion/provenance/classification implementation

Status: **3B implemented; the 3A inventory above is unchanged and still accurate
as the pre-3B baseline.** This addendum records the files 3B added or changed so
the same completeness guard keeps covering the classification sweep. 3B does not
remove `MaterialFamily.BaseUnitId` or any family contract - that is 3C.

### New sources introduced by 3B

- `src/Domain/Materials/MaterialClassification.cs` — the single server-side
  kind/tracking matrix and the derived `RequiresAssetNumber` rule; consumed by the
  material create/update handlers and the material read projections.
- `src/Domain/DocumentLines/DocumentLineProvenance.cs` — the immutable
  per-line snapshot of material catalog version, kind, tracking type, base unit,
  and the conversion identity/factor that produced the line's base quantity.
- `src/Domain/DocumentLines/DocumentLine.cs` — stores that snapshot
  (`Source*` fields, `Provenance`) and captures it in `Create`/`Update` while the
  owning document is still Draft.
- `src/Application/DocumentLines/DocumentLineProvenanceRules.cs` — the pure
  submit/post re-validation of the captured snapshot (version, base unit,
  classification, conversion identity and factor).
- `src/Application/Materials/MaterialOperationalUseGuard.cs` — the
  lock-after-operational-use test that blocks a kind/tracking change once a
  non-draft document line or a posted stock movement references the material.
- `src/Infrastructure/DocumentLines/DocumentLineConfiguration.cs` — maps the
  `source_*` provenance columns and adds the two provenance check constraints plus
  the `source_conversion_id` / `(document_id, source_material_version)` indexes.

### 3B tests added to the classification sweep

- `tests/Application.UnitTests/Materials/MaterialClassificationProvenanceTests.cs`
- `tests/Application.UnitTests/DocumentLines/DocumentLineProvenanceTests.cs`
- `tests/Application.UnitTests/InventoryBalances/LowStockInventoryBalancesQueryHandlerTests.cs` — exercises material classification when projecting low-stock balance settings.
- `tests/Application.UnitTests/UnitsOfMeasure/SetUnitOfMeasureStatusCommandHandlerTests.cs` — verifies status changes against material classification/use.
- `tests/Application.UnitTests/MaterialUnitConversions/MaterialUnitConversionProvenanceTests.cs`
- `tests/IntegrationTests/M4/MaterialProvenanceDocumentApiTests.cs`
- `tests/IntegrationTests/M4/CompleteWarehouseDocumentDraftApiTests.cs` — creates typed complete-draft material lines and quantity adjustments.
- `tests/IntegrationTests/Infrastructure/MaterialProvenanceMigrationSafetyTests.cs`
- `tests/ArchitectureTests/MaterialProvenanceContractTests.cs` — the 3B gates for
  the semantic string enums, for provenance capture on every production
  document-line write, and for "no family base unit in quantity paths".

### 3B consequences for the readers listed above

- `Material.BaseUnitId` is now the only base unit the document-line and adjustment
  write paths read: `DocumentLineCatalogResolver` joins the base unit on
  `m.BaseUnitId`, `BaseQuantityCalculator` takes a `materialBaseUnitId` argument,
  `DocumentLineSubmissionValidator` validates against `Material.BaseUnitId`, and
  the `AddAdjustmentLine`/`UpdateAdjustmentLine`/`CreateDisposal`/
  `CreateFromCount` handlers pass or project the material value. The family
  property, its column, and its create/read DTOs are untouched and remain 3C
  scope; after 3B the family value has no production quantity-semantics reader
  left, which is the precondition 3C's gate checks.
- `RequiresAssetNumber` is derived from `MaterialKind` and no longer stored, so
  the material read projections derive it and
  `CreateAdjustmentFromCountCommandHandler` filters on `MaterialKind` alone.
- Material create/update request bodies bind `MaterialKind`/`TrackingType` as
  semantic string enums (`Consumable|Durable|Asset`, `Quantity|Serial`) in
  `src/Web.Api/Controllers/Materials/CreateMaterialController.cs` and
  `src/Web.Api/Controllers/Materials/UpdateMaterialController.cs`; the checked-in
  OpenAPI snapshot was regenerated for that change only.
- `AddMaterialConversionCommandHandler` is unchanged: it already pinned the
  conversion target to the material base unit, which is now the only base unit
  source of truth. 3B additionally blocks a factor change or removal of a
  conversion whose provenance is used by a submitted/posted document or a posted
  movement.
- `tests/IntegrationTests/Performance/InventoryLedgerRecoveryIntegrationTests.cs`
  covers immutable-ledger balance rebuild and concurrent adjustment/reversal parity;
  it creates submitted document lines with an explicit material provenance snapshot.
