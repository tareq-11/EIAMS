#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "$0")/.." && pwd)"
output="${RBAC_V2_OUTPUT:-$root/contracts/baseline/rbac-v2-preflight.json}"
output_dir="$(dirname -- "$output")"
[[ "$output" = /* ]] || { echo "Refusing: output must be an absolute path." >&2; exit 3; }
mkdir -p -- "$output_dir"
tmp_dir="$(mktemp -d)"; artifact_tmp=''
cleanup() { rm -rf -- "$tmp_dir"; [[ -z "$artifact_tmp" ]] || rm -f -- "$artifact_tmp"; }
trap cleanup EXIT
export LC_ALL=C
if [[ -n "${RBAC_V2_PGHOST:-}" || -n "${RBAC_V2_PGDATABASE:-}" || -n "${RBAC_V2_PGUSER:-}" || -n "${RBAC_V2_PGPASSWORD:-}" ]]; then
  [[ "${RBAC_V2_ALLOW_NON_PRODUCTION:-}" == "1" ]] || { echo "Refusing: explicit non-production confirmation required." >&2; exit 3; }
  database_name_lower="${RBAC_V2_PGDATABASE:-}"; database_name_lower="${database_name_lower,,}"
  [[ -n "${RBAC_V2_PGHOST:-}" && "${RBAC_V2_PGHOST,,}" != *neon* && "$database_name_lower" != *production* ]] || { echo "Refusing: non-production, non-Neon target required." >&2; exit 3; }
fi
expected=(database active_marker_not_dotted legacy_permission_rows unknown_permission_rows missing_legacy_mappings legacy_role_grants legacy_allowed_scope_rows legacy_role_grant_parity legacy_scope_parity dotted_grants_without_scope seeded_role_grant_drift seeded_scope_drift unknown_mapping_targets wildcard_permission_rows)
status=UNAVAILABLE; note='RBAC_V2_PG* variables were not supplied; database checks were not inferred.'; rows='[]'
if [[ -n "${RBAC_V2_PGHOST:-}" ]]; then
  : "${RBAC_V2_PGDATABASE:?RBAC_V2_PGDATABASE is required}"; : "${RBAC_V2_PGUSER:?RBAC_V2_PGUSER is required}"
  if PGOPTIONS='-c default_transaction_read_only=on -c statement_timeout=10000 -c lock_timeout=2000' PGHOST="$RBAC_V2_PGHOST" PGPORT="${RBAC_V2_PGPORT:-5432}" PGDATABASE="$RBAC_V2_PGDATABASE" PGUSER="$RBAC_V2_PGUSER" PGPASSWORD="${RBAC_V2_PGPASSWORD:-}" psql -X -v ON_ERROR_STOP=1 -At -F '|' -f "$root/scripts/rbac-v2-preflight.sql" >"$tmp_dir/db.txt" 2>"$tmp_dir/db.err"; then
    printf '%s\n' "${expected[@]}" | sort >"$tmp_dir/expected"; cut -d'|' -f1 "$tmp_dir/db.txt" | sort >"$tmp_dir/actual"; valid=true
    awk -F'|' 'NF != 4 || $1 == "" || $2 !~ /^(PASS|WARN|BLOCK)$/ || $3 !~ /^[0-9]+$/ { exit 1 }' "$tmp_dir/db.txt" || valid=false
    [[ -z "$(uniq -d "$tmp_dir/actual")" ]] || valid=false; diff -q "$tmp_dir/expected" "$tmp_dir/actual" >/dev/null || valid=false
    if [[ "$valid" == true ]]; then rows="$(jq -Rn '[inputs | split("|") | {section:.[0],status:.[1],count:(.[2]|tonumber),note:.[3]}]' <"$tmp_dir/db.txt")"; status=PASS; note='Read-only RBAC checks completed with the complete expected section set.'; else note='RBAC SQL output was malformed, duplicate, missing, or unexpected; status is unavailable.'; fi
  else note='RBAC SQL execution failed; no PASS was inferred.'; fi
fi
overall=WARN
if [[ "$status" == PASS ]]; then if jq -e 'any(.[]; .status == "BLOCK")' >/dev/null <<<"$rows"; then overall=BLOCK; elif jq -e 'any(.[]; .status == "WARN")' >/dev/null <<<"$rows"; then overall=WARN; else overall=PASS; fi; fi
artifact_tmp="$(mktemp "$output_dir/.rbac-v2-preflight.XXXXXX")"
jq -n --arg overall "$overall" --arg status "$status" --arg note "$note" --argjson checks "$rows" '{version:"phase-2a-v1",overallStatus:$overall,semantics:{PASS:"dotted-v1 parity is clean",BLOCK:"cutover is blocked and no cleanup is approved",WARN:"review required or database unavailable",UNAVAILABLE:"not safely inferable"},database:{status:$status,note:$note,checks:$checks},limitations:["Read-only counts only; no identifiers, secrets, response bodies, or permission derivation are emitted.","Permission mappings are audit history and never grant authorization.","A missing target/schema is unavailable, never PASS."]}' >"$artifact_tmp"
mv -f -- "$artifact_tmp" "$output"
artifact_tmp=''
echo "RBAC v2 preflight written: ${output#$root/} (status=$overall)"
[[ "$overall" == BLOCK ]] && exit 2
[[ "$overall" == WARN ]] && exit 4
