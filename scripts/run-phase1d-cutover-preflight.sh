#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "$0")/.." && pwd)"
output="${EIAMS_PHASE1D_OUTPUT:-$root/contracts/baseline/phase1d-cutover-preflight.json}"
output_dir="$(dirname -- "$output")"
[[ "$output" = /* ]] || { echo "Refusing: output must be an explicit absolute path." >&2; exit 3; }
mkdir -p -- "$output_dir"
tmp_dir="$(mktemp -d)"; artifact_tmp=''
cleanup() { rm -rf -- "$tmp_dir"; [[ -z "$artifact_tmp" ]] || rm -f -- "$artifact_tmp"; }
trap cleanup EXIT
export LC_ALL=C
if [[ -n "${EIAMS_PHASE1D_PGHOST:-}" || -n "${EIAMS_PHASE1D_PGDATABASE:-}" || -n "${EIAMS_PHASE1D_PGUSER:-}" || -n "${EIAMS_PHASE1D_PGPASSWORD:-}" ]]; then
  [[ "${EIAMS_PHASE1D_ALLOW_NON_PRODUCTION:-}" == "1" ]] || { echo "Refusing: set EIAMS_PHASE1D_ALLOW_NON_PRODUCTION=1 for an approved non-production restore." >&2; exit 3; }
  database_name_lower="${EIAMS_PHASE1D_PGDATABASE:-}"; database_name_lower="${database_name_lower,,}"
  [[ -n "${EIAMS_PHASE1D_PGHOST:-}" && "${EIAMS_PHASE1D_PGHOST,,}" != *neon* && "$database_name_lower" != *production* ]] || { echo "Refusing: only an explicit non-production, non-Neon target is permitted." >&2; exit 3; }
fi
expected=(database organizational_unit_user_assignments organizational_unit_role_allowed_scopes organizational_unit_permission_allowed_scopes users_without_exactly_one_assignment invalid_assignment_scope_id role_scope_incompatibility)
db_status=UNAVAILABLE; db_note='EIAMS_PHASE1D_PG* variables were not supplied; database checks were not inferred.'; db_rows='[]'
if [[ -n "${EIAMS_PHASE1D_PGHOST:-}" ]]; then
  : "${EIAMS_PHASE1D_PGDATABASE:?EIAMS_PHASE1D_PGDATABASE is required}"; : "${EIAMS_PHASE1D_PGUSER:?EIAMS_PHASE1D_PGUSER is required}"
  if PGOPTIONS='-c default_transaction_read_only=on -c statement_timeout=10000 -c lock_timeout=2000' PGHOST="$EIAMS_PHASE1D_PGHOST" PGPORT="${EIAMS_PHASE1D_PGPORT:-5432}" PGDATABASE="$EIAMS_PHASE1D_PGDATABASE" PGUSER="$EIAMS_PHASE1D_PGUSER" PGPASSWORD="${EIAMS_PHASE1D_PGPASSWORD:-}" psql -X -v ON_ERROR_STOP=1 -At -F '|' -f "$root/scripts/phase1d-cutover-preflight.sql" >"$tmp_dir/db.txt" 2>"$tmp_dir/db.err"; then
    printf '%s\n' "${expected[@]}" | sort >"$tmp_dir/expected"; cut -d'|' -f1 "$tmp_dir/db.txt" | sort >"$tmp_dir/actual"; valid=true
    awk -F'|' 'NF != 4 || $1 == "" || $2 !~ /^(PASS|WARN|BLOCK)$/ || $3 !~ /^[0-9]+$/ { exit 1 }' "$tmp_dir/db.txt" || valid=false
    [[ -z "$(uniq -d "$tmp_dir/actual")" ]] || valid=false; diff -q "$tmp_dir/expected" "$tmp_dir/actual" >/dev/null || valid=false
    if [[ "$valid" == true ]]; then db_rows="$(jq -Rn '[inputs | split("|") | {section:.[0],status:.[1],count:(.[2]|tonumber),note:.[3]}]' <"$tmp_dir/db.txt")"; db_status=PASS; db_note='Read-only PostgreSQL checks completed with the complete expected section set.'; else db_note='PostgreSQL returned malformed, duplicate, missing, or unexpected sections; database status is not inferred.'; fi
  else db_note='PostgreSQL preflight failed; schema/query execution status is unavailable and no PASS is inferred.'; fi
fi
overall=WARN
if [[ "$db_status" == PASS ]]; then
  if jq -e 'any(.[]; .status == "BLOCK")' >/dev/null <<<"$db_rows"; then overall=BLOCK; elif jq -e 'any(.[]; .status == "WARN")' >/dev/null <<<"$db_rows"; then overall=WARN; else overall=PASS; fi
fi
artifact_tmp="$(mktemp "$output_dir/.phase1d-cutover-preflight.XXXXXX")"
jq -n --arg overall "$overall" --arg status "$db_status" --arg note "$db_note" --argjson checks "$db_rows" '{version:"phase-1d-v1",overallStatus:$overall,semantics:{PASS:"no observed anomaly",BLOCK:"manual remediation or migration gate required",WARN:"review required or database unavailable",UNAVAILABLE:"not safely inferable"},database:{status:$status,note:$note,checks:$checks},remediation:"OrganizationalUnit user assignments are a BLOCK. A business owner must explicitly choose Enterprise, Site, Warehouse, or suspend the user after backup/restore verification; this tool never maps or deletes rows.",limitations:["Counts only; no identifiers, PII, credentials, or live row data are written.","SQL assumes the checked-in EF table/column contract; missing schema is UNAVAILABLE.","No migration or data mutation is performed."]}' >"$artifact_tmp"
mv -f -- "$artifact_tmp" "$output"; artifact_tmp=''
echo "Phase 1D preflight written: ${output#$root/} (status=$overall)"
[[ "$overall" == BLOCK ]] && exit 2; [[ "$overall" == WARN ]] && exit 4
