#!/usr/bin/env bash
set -euo pipefail

# Phase 0A is read-only. It produces a deterministic source/schema inventory and,
# when explicitly pointed at an approved non-production PostgreSQL target, sanitized
# database counts. It never creates/changes tables, rows, indexes, or migrations.

root="$(cd -- "$(dirname -- "$0")/.." && pwd)"
output="${EIAMS_BASELINE_OUTPUT:-$root/contracts/baseline/phase0-baseline.json}"
output_dir="$(dirname -- "$output")"
mkdir -p -- "$output_dir"

if [[ -n "${EIAMS_BASELINE_PGHOST:-}" || -n "${EIAMS_BASELINE_PGDATABASE:-}" || -n "${EIAMS_BASELINE_PGUSER:-}" || -n "${EIAMS_BASELINE_PGPASSWORD:-}" ]]; then
  if [[ "${EIAMS_BASELINE_ALLOW_NON_PRODUCTION:-}" != "1" ]]; then
    echo "Refusing: set EIAMS_BASELINE_ALLOW_NON_PRODUCTION=1 for an approved non-production restore." >&2
    exit 3
  fi
  host="${EIAMS_BASELINE_PGHOST:-}"
  database_name_lower="${EIAMS_BASELINE_PGDATABASE:-}"
  database_name_lower="${database_name_lower,,}"
  if [[ -z "$host" || "${host,,}" == *neon* || "$database_name_lower" == *production* ]]; then
    echo "Refusing: only an explicit non-production, non-Neon PostgreSQL target is permitted." >&2
    exit 3
  fi
fi

tmp="$(mktemp -d)"
artifact_tmp=''
cleanup() {
  rm -rf -- "$tmp"
  [[ -z "$artifact_tmp" ]] || rm -f -- "$artifact_tmp"
}
trap cleanup EXIT
export LC_ALL=C

expected_db_sections=(
  database users_without_exactly_one_assignment organizational_unit_user_assignments
  organizational_unit_role_allowed_scopes organizational_unit_permission_allowed_scopes
  username_null_or_blank username_noncanonical username_invalid_shape_or_length
  username_legacy_generated username_duplicate_normalized legacy_colon_permissions non_dotted_non_legacy_permissions
  material_family_missing_base_unit material_missing_or_invalid_base_unit
  conversion_base_unit_mismatch non_supplier_receiving_info document_invalid_row_version
  posted_document_missing_metadata in_progress_count_missing_membership
  invalid_document_sequence_facts duplicate_document_sequence_keys
)

if command -v rg >/dev/null 2>&1; then
  (cd "$root" && rg --files src/Web.Api/Controllers | sort) >"$tmp/routes.txt"
  (cd "$root" && rg --files src/Application | sort) >"$tmp/application-files.txt"
  (cd "$root" && rg --files src/Application src/Web.Api | rg 'Request|Response|Command|Query|Dto|DTO|ApiContracts|ApiResults|OpenApi' | sort -u) >"$tmp/dto-files.txt"
  (cd "$root" && rg -l 'PermissionCodes|HasPermission' src | sort) >"$tmp/permission-files.txt"
else
  # Hosted runners need not include ripgrep. Git lists tracked and non-ignored
  # untracked sources without accidentally inventorying bin/obj outputs.
  (cd "$root" && git ls-files -co --exclude-standard -- src/Web.Api/Controllers | sort) >"$tmp/routes.txt"
  (cd "$root" && git ls-files -co --exclude-standard -- src/Application | sort) >"$tmp/application-files.txt"
  (cd "$root" && git ls-files -co --exclude-standard -- src/Application src/Web.Api | grep -E 'Request|Response|Command|Query|Dto|DTO|ApiContracts|ApiResults|OpenApi' | sort -u) >"$tmp/dto-files.txt"
  (cd "$root" && git ls-files -z -co --exclude-standard -- src | xargs -0 -r grep -lE 'PermissionCodes|HasPermission' | sort) >"$tmp/permission-files.txt"
fi

json_lines() {
  jq -Rn '[inputs | select(length > 0)]' <"$1"
}

route_json="$(json_lines "$tmp/routes.txt")"
application_json="$(json_lines "$tmp/application-files.txt")"
dto_json="$(json_lines "$tmp/dto-files.txt")"
permission_json="$(json_lines "$tmp/permission-files.txt")"
openapi="$root/contracts/openapi/eiams-backend-v1.openapi.json"
openapi_routes="$(jq '[.paths // {} | to_entries[] as $path | $path.value | to_entries[] | select(.key|IN("get","post","put","patch","delete","options","head","trace")) | {path:$path.key,method:(.key|ascii_upcase)}] | sort_by(.path,.method)' "$openapi")"
openapi_schemas="$(jq '[.components.schemas // {} | keys[]] | sort' "$openapi")"

if command -v rg >/dev/null 2>&1; then
  (cd "$root" && rg -o '"[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+"' src/Application/Abstractions/Authorization/PermissionCodes.cs src/Domain/Permissions/WellKnownDottedPermissions.cs 2>/dev/null | sed -E 's/.*"([^"]+)".*/\1/' | sort -u) >"$tmp/permission-codes.txt"
else
  (cd "$root" && grep -hEo '"[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+"' src/Application/Abstractions/Authorization/PermissionCodes.cs src/Domain/Permissions/WellKnownDottedPermissions.cs | sed -E 's/.*"([^"]+)".*/\1/' | sort -u) >"$tmp/permission-codes.txt"
fi
permission_codes="$(json_lines "$tmp/permission-codes.txt")"

db_rows=''
db_status='UNAVAILABLE'
db_note='EIAMS_BASELINE_PG* variables were not supplied; database checks were not inferred.'
if [[ -n "${EIAMS_BASELINE_PGHOST:-}" ]]; then
  : "${EIAMS_BASELINE_PGDATABASE:?EIAMS_BASELINE_PGDATABASE is required when database checks are enabled}"
  : "${EIAMS_BASELINE_PGUSER:?EIAMS_BASELINE_PGUSER is required when database checks are enabled}"
  if PGOPTIONS='-c default_transaction_read_only=on -c statement_timeout=10000 -c lock_timeout=2000' \
      PGHOST="$EIAMS_BASELINE_PGHOST" PGPORT="${EIAMS_BASELINE_PGPORT:-5432}" \
      PGDATABASE="$EIAMS_BASELINE_PGDATABASE" PGUSER="$EIAMS_BASELINE_PGUSER" \
      PGPASSWORD="${EIAMS_BASELINE_PGPASSWORD:-}" \
      psql -X -v ON_ERROR_STOP=1 -At -F '|' \
      -f "$root/scripts/phase0-baseline-preflight.sql" >"$tmp/db.txt" 2>"$tmp/db.err"; then
    printf '%s\n' "${expected_db_sections[@]}" | sort >"$tmp/expected-sections.txt"
    cut -d'|' -f1 "$tmp/db.txt" | sort >"$tmp/actual-sections.txt"
    valid_db_output=true
    awk -F'|' 'NF != 4 || $1 == "" || $2 !~ /^(PASS|WARN|BLOCK)$/ || $3 !~ /^[0-9]+$/ { exit 1 }' "$tmp/db.txt" || valid_db_output=false
    [[ -z "$(uniq -d "$tmp/actual-sections.txt")" ]] || valid_db_output=false
    diff -q "$tmp/expected-sections.txt" "$tmp/actual-sections.txt" >/dev/null || valid_db_output=false
    if [[ "$valid_db_output" == true ]]; then
      db_rows="$(jq -Rn '[inputs | split("|") | {section:.[0],status:.[1],count:(.[2]|tonumber),note:.[3]}]' <"$tmp/db.txt")"
      db_status="PASS"
      db_note="Read-only PostgreSQL checks completed and returned the complete expected section set."
    else
      db_status="UNAVAILABLE"
      db_rows='[]'
      db_note="PostgreSQL returned malformed, duplicate, missing, or unexpected preflight sections; no database status was inferred."
    fi
  else
    echo "Database preflight failed; sanitized error follows:" >&2
    sed -E 's/(postgres(ql)?:\/\/)[^@]+@/\1[redacted]@/g' "$tmp/db.err" >&2
    db_status="UNAVAILABLE"
    db_note="PostgreSQL preflight could not complete; no database status was inferred."
    db_rows='[]'
  fi
else
  db_rows='[]'
fi

if [[ "$db_status" == "UNAVAILABLE" ]]; then
  overall="WARN"
elif jq -e 'any(.[]; .status == "BLOCK")' >/dev/null 2>&1 <<<"$db_rows"; then
  overall="BLOCK"
elif jq -e 'any(.[]; .status == "WARN")' >/dev/null 2>&1 <<<"$db_rows"; then
  overall="WARN"
else
  overall="PASS"
fi

artifact_tmp="$(mktemp "$output_dir/.phase0-baseline.XXXXXX")"
jq -n --arg version "phase-0a-v1" --arg overall "$overall" \
  --arg db_status "$db_status" --arg db_note "$db_note" \
  --argjson routes "$route_json" --argjson application_files "$application_json" --argjson dto_files "$dto_json" \
  --argjson permission_files "$permission_json" --argjson db_checks "$db_rows" \
  --argjson openapi_routes "$openapi_routes" --argjson openapi_schemas "$openapi_schemas" \
  --argjson permission_codes "$permission_codes" \
  '{version:$version,overallStatus:$overall,semantics:{PASS:"no observed anomaly",BLOCK:"release-blocking preflight anomaly",WARN:"review required or check unavailable",UNAVAILABLE:"not safely inferable without target/schema"},sourceInventory:{controllerFiles:$routes,applicationFiles:$application_files,dtoContractFiles:$dto_files,permissionFiles:$permission_files,permissionCodes:{values:$permission_codes,source:"src/Application/Abstractions/Authorization/PermissionCodes.cs and src/Domain/Permissions/WellKnownDottedPermissions.cs"},openApi:{routes:$openapi_routes,schemaNames:$openapi_schemas}},database:{status:$db_status,note:$db_note,checks:$db_checks},limitations:["Counts are sanitized and contain no row identifiers or secrets.","A missing database target is WARN, not evidence of a clean database.","NFKC normalization is not reproduced by this SQL preflight; canonical ASCII checks are explicit.","This artifact is an inventory/preflight only; it does not approve remediation or migration."]}' \
  >"$artifact_tmp"

mv -f -- "$artifact_tmp" "$output"
artifact_tmp=''

echo "Phase 0A baseline written: ${output#$root/} (status=$overall)"
[[ "$overall" == "BLOCK" ]] && exit 2
[[ "$overall" == "WARN" ]] && exit 4
