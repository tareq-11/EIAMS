#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
inventory="$root_dir/docs/phase3a-material-base-unit-inventory.md"

[[ -f "$inventory" ]] || { echo "missing Phase 3A inventory: $inventory" >&2; exit 1; }

required_paths=(
  "src/Domain/Materials/Material.cs"
  "src/Infrastructure/Materials/MaterialConfiguration.cs"
  "src/Domain/MaterialFamilies/MaterialFamily.cs"
  "src/Infrastructure/MaterialFamilies/MaterialFamilyConfiguration.cs"
  "src/Application/DocumentLines/DocumentLineCatalogResolver.cs"
  "src/Application/DocumentLines/BaseQuantityCalculator.cs"
  "src/Application/MaterialUnitConversions/Add/AddMaterialUnitConversionCommandHandler.cs"
  "src/Application/InventoryAdjustments/CreateDisposal/CreateDisposalCommandHandler.cs"
  "src/Application/InventoryAdjustments/CreateFromCount/CreateAdjustmentFromCountCommandHandler.cs"
  "src/Infrastructure/WarehouseDocuments/PostingMaterialCatalogLoader.cs"
  "src/Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs"
  "src/Web.Api/Controllers/MaterialFamilies/CreateMaterialFamilyController.cs"
  "contracts/openapi/eiams-backend-v1.openapi.json"
  "tests/IntegrationTests/Performance/SyntheticDatasetSeeder.cs"
)

for path in "${required_paths[@]}"; do
  [[ -f "$root_dir/$path" ]] || { echo "inventory target does not exist: $path" >&2; exit 1; }
  rg -Fq "$path" "$inventory" || { echo "inventory omission: $path" >&2; exit 1; }
done

# Classification blast radius: ensure every current source/test file that uses
# these material fields remains named in the inventory. Migration designers are
# generated historical snapshots and are intentionally excluded from consumers.
# Domain definition files (MaterialKind.cs, TrackingType.cs, Material.cs,
# DocumentLineType.cs) are excluded as they DEFINE the classification vocabulary,
# not consume it; they appear in grep due to enum/entity declarations or doc comments.
while IFS= read -r path; do
  [[ -n "$path" ]] || continue
  rg -Fq "$path" "$inventory" || { echo "classification inventory omission: $path" >&2; exit 1; }
done < <(rg -l --glob '!Migrations/**' --glob '!**/Migrations/**' \
  'MaterialKind|TrackingType|RequiresAssetNumber|IsAssetTracked' \
  "$root_dir/src/Domain" "$root_dir/src/Application" "$root_dir/src/Infrastructure" \
  "$root_dir/src/Web.Api" "$root_dir/tests" \
  | grep -v 'src/Domain/Materials/MaterialKind\.cs$' \
  | grep -v 'src/Domain/Materials/TrackingType\.cs$' \
  | grep -v 'src/Domain/Materials/Material\.cs$' \
  | grep -v 'src/Domain/Common/DocumentLineType\.cs$' \
  | sed "s#^$root_dir/##" | sort -u)

# Prose assertions run against a whitespace-normalized copy of the inventory.
# The document is hard-wrapped, so a single claim such as "requiresAssetNumber is
# response-only" is split across two source lines; a line-oriented `rg -q` can
# never match it and, under `set -e`, fails the script with no diagnostic at all.
# Collapsing all whitespace to single spaces makes the match independent of
# wrapping; bounded `.{}` gaps keep a two-term assertion from being satisfied by
# two unrelated terms hundreds of lines apart in the flattened text.
normalized_inventory="$(tr '\n' ' ' <"$inventory" | tr -s '[:space:]' ' ')"

require_phrase() {
  local pattern="$1"
  local description="$2"
  if ! grep -Eq -- "$pattern" <<<"$normalized_inventory"; then
    {
      echo "inventory prose assertion failed: $description" >&2
      echo "  pattern: $pattern" >&2
      echo "  file:    ${inventory#"$root_dir"/} (compared with newlines collapsed to spaces)" >&2
    } >&2
    exit 1
  fi
}

require_phrase "inventory complete; 3A gate recorded" "3A status header must record the gate"
require_phrase "No migration file is modified" "3A gate must record that migrations are untouched"
require_phrase "Material\.BaseUnitId" "decision boundary must name Material.BaseUnitId as the source of truth"
require_phrase "MaterialFamily\.BaseUnitId" "decision boundary must name MaterialFamily.BaseUnitId as the legacy source"
require_phrase "Classification inventory \(decision 032" "classification inventory section must be present"
require_phrase "requiresAssetNumber.{0,40}response-only" \
  "material response schemas must document requiresAssetNumber as response-only"

echo "Phase 3A material base-unit inventory: PASS"
