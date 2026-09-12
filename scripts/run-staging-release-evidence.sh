#!/usr/bin/env bash
# Closed, explicitly gated evidence collection for an already approved HTTPS staging deployment.
# Credentials, tokens, URLs, SQL output, and response bodies never leave the private run directory.
set -euo pipefail

readonly gate=RUN_STAGING_RELEASE_EVIDENCE mutation_gate=RUN_STAGING_RELEASE_MUTATIONS
readonly confirmation_phrase=I_UNDERSTAND_THIS_CHANGES_APPROVED_STAGING
readonly api_url="${EIAMS_STAGING_API_URL:-}" approved_host="${EIAMS_STAGING_APPROVED_HOST:-}"
readonly db_host="${EIAMS_STAGING_DB_HOST:-}" approved_db_host="${EIAMS_STAGING_APPROVED_DB_HOST:-}" db_port="${EIAMS_STAGING_DB_PORT:-5432}"
readonly db_name="${EIAMS_STAGING_DB_NAME:-}" db_user="${EIAMS_STAGING_DB_USER:-}" db_password="${EIAMS_STAGING_DB_PASSWORD:-}"
readonly db_root_cert="${EIAMS_STAGING_DB_ROOT_CERT:-}"
readonly evidence_dir="${EIAMS_STAGING_EVIDENCE_DIR:-}" admin_email="${EIAMS_STAGING_ADMIN_EMAIL:-}" admin_password="${EIAMS_STAGING_ADMIN_PASSWORD:-}"
refuse() { echo "Refusing staging release evidence: $1" >&2; exit 3; }
for tool in bash curl jq psql getent mktemp mv date realpath awk sort cat mkdir rm; do command -v "$tool" >/dev/null 2>&1 || refuse "required tool is unavailable"; done
[[ "${!gate:-}" == 1 ]] || refuse "set $gate=1 after approving an isolated staging target"
[[ -n "$api_url" && -n "$approved_host" && -n "$db_host" && -n "$approved_db_host" && -n "$db_name" && -n "$db_user" && -n "$db_password" && -n "$evidence_dir" ]] || refuse "API, approved hosts, separate DB connection values, and evidence directory are required"
[[ "$db_port" =~ ^[0-9]{1,5}$ && $((10#$db_port)) -ge 1 && $((10#$db_port)) -le 65535 ]] || refuse "database port is invalid"
[[ "$api_url" =~ ^https://([A-Za-z0-9][A-Za-z0-9.-]*[A-Za-z0-9])(:[0-9]{1,5})?/?$ ]] || refuse "API URL must be a bare HTTPS origin without credentials, path, query, or fragment"
api_port="${BASH_REMATCH[2]#:}"; [[ -z "$api_port" || ( $((10#$api_port)) -ge 1 && $((10#$api_port)) -le 65535 ) ]] || refuse "API port is invalid"
api_host="${BASH_REMATCH[1],,}"; normalized_approved_host="${approved_host,,}"; normalized_db_host="${db_host,,}"; normalized_approved_db_host="${approved_db_host,,}"
[[ "$api_host" == "$normalized_approved_host" ]] || refuse "API hostname does not exactly match approved hostname"
[[ "$normalized_db_host" == "$normalized_approved_db_host" ]] || refuse "database hostname does not exactly match approved database hostname"
is_staging_host() { [[ "$1" =~ (^|[.-])staging([.-]|$) ]] && [[ ! "$1" =~ (localhost|neon|production|prod) ]] && [[ ! "$1" =~ ^[0-9.:]+$ ]]; }
is_staging_host "$api_host" || refuse "approved hostname must be a non-production hostname with a distinct staging marker"
is_staging_host "$normalized_db_host" || refuse "database hostname must be a non-production hostname with a distinct staging marker"
is_private_address() { [[ "$1" == 127.* || "$1" == 10.* || "$1" == 0.* || "$1" == 169.254.* || "$1" == 192.168.* || "$1" =~ ^172\.(1[6-9]|2[0-9]|3[0-1])\. || "$1" == ::1 || "$1" =~ ^f[cd] || "$1" =~ ^fe80: ]]; }
validate_dns() { local addresses address; addresses="$(getent ahosts "$1" 2>/dev/null | awk '{print $1}' | sort -u || true)"; [[ -n "$addresses" ]] || refuse "approved hostname did not resolve"; while IFS= read -r address; do [[ -z "$address" ]] || ! is_private_address "$address" || refuse "approved hostname resolved to a private or loopback address"; done <<<"$addresses"; }
validate_dns "$api_host"; validate_dns "$normalized_db_host"
[[ -z "$db_root_cert" || -r "$db_root_cert" ]] || refuse "optional database root certificate is not readable"
tmp_root="$(realpath -e "${TMPDIR:-/tmp}")" || refuse "temporary root is unavailable"; expected_evidence_dir="$tmp_root/eiams-staging-evidence"
mkdir -p -- "$expected_evidence_dir"; evidence_dir_real="$(realpath -e "$evidence_dir")" || refuse "evidence directory is unavailable"
[[ "$evidence_dir_real" == "$expected_evidence_dir" ]] || refuse "evidence directory must be the dedicated temporary staging evidence subtree"
umask 077; run_dir="$(mktemp -d "$expected_evidence_dir/.run.XXXXXX")"; cleanup() { rm -rf -- "$run_dir"; }; trap cleanup EXIT
declare -a step_names=() step_statuses=() step_outcomes=() step_durations=(); overall_status=pass
record_step() { step_names+=("$1"); step_statuses+=("$2"); step_outcomes+=("$3"); step_durations+=("$4"); [[ "$2" == pass ]] || overall_status=fail; }
write_evidence() { local target temporary i mode status gate_satisfied; target="$expected_evidence_dir/staging-release-evidence-$(date -u +%Y%m%dT%H%M%SZ)-$RANDOM.json"; temporary="$(mktemp "$expected_evidence_dir/.evidence.XXXXXX")"; if [[ "$overall_status" != pass ]]; then mode="$([[ "${!mutation_gate:-}" == 1 ]] && echo full_staging_smoke || echo read_only_preflight)"; status=failed; gate_satisfied=false; elif [[ "${!mutation_gate:-}" == 1 ]]; then mode=full_staging_smoke; status=passed; gate_satisfied=true; else mode=read_only_preflight; status=preflight_passed; gate_satisfied=false; fi; { printf '{"scope":"explicit approved HTTPS staging target only","mode":"%s","status":"%s","releaseGateSatisfied":%s,"steps":[' "$mode" "$status" "$gate_satisfied"; for i in "${!step_names[@]}"; do [[ "$i" == 0 ]] || printf ','; printf '{"name":"%s","status":"%s","outcome":"%s","durationMilliseconds":%s}' "${step_names[$i]}" "${step_statuses[$i]}" "${step_outcomes[$i]}" "${step_durations[$i]}"; done; printf '],"limitations":["Forwarded-header and proxy behavior was not observed by this black-box runner.","No identifiers, URLs, credentials, tokens, SQL, or response bodies are recorded."],"mutationsRequested":%s}\n' "$([[ "${!mutation_gate:-}" == 1 ]] && echo true || echo false)"; } >"$temporary"; mv -f -- "$temporary" "$target"; }
curl_request() { local method="$1" path="$2" body="$3" token="$4" input="${5:-}" key_file="${6:-}" config="$run_dir/curl-$RANDOM.conf" result="$run_dir/curl-$RANDOM.result"; { printf 'url = "%s%s"\nrequest = "%s"\nsilent\nshow-error\nfail-early\nconnect-timeout = 10\nmax-time = 30\nproto = "=https"\nheader = "Accept: application/json"\n' "${api_url%/}" "$path" "$method"; [[ -z "$token" ]] || printf 'header = "Authorization: Bearer %s"\n' "$token"; [[ -z "$key_file" ]] || printf 'header = "Idempotency-Key: %s"\n' "$(<"$key_file")"; [[ -z "$input" ]] || printf 'header = "Content-Type: application/json"\ndata-binary = "@%s"\n' "$input"; printf 'output = "%s"\nwrite-out = "%%{http_code}\\t%%{time_total}"\n' "$body"; } >"$config"; curl --config "$config" >"$result" 2>"$run_dir/curl.stderr" || return 1; [[ -s "$result" ]] && cat "$result"; }
run_http_step() { local name="$1" method="$2" path="$3" wanted="$4" contract="$5" token="${6:-}" input="${7:-}" key_file="${8:-}" body start end result status; body="$run_dir/$name.body"; start="$(date +%s%3N)"; if ! result="$(curl_request "$method" "$path" "$body" "$token" "$input" "$key_file")"; then end="$(date +%s%3N)"; record_step "$name" fail transport "$((end-start))"; return 1; fi; status="${result%%$'\t'*}"; end="$(date +%s%3N)"; if [[ "$status" != "$wanted" ]] || ! jq -e "$contract" "$body" >/dev/null 2>&1; then record_step "$name" fail contract "$((end-start))"; return 1; fi; record_step "$name" pass "http_$status" "$((end-start))"; }
db_preflight() {
  local sql="$run_dir/runtime-role-preflight.sql" start end result
  start="$(date +%s%3N)"
  # Catalog/ACL-only inspection. The connection itself is forced read-only and time bounded.
  cat >"$sql" <<'SQL'
WITH RECURSIVE inherited_roles(roleid) AS (
  SELECT m.roleid FROM pg_auth_members m WHERE m.member = (SELECT oid FROM pg_roles WHERE rolname = current_user)
  UNION
  SELECT m.roleid FROM pg_auth_members m JOIN inherited_roles i ON m.member = i.roleid
)
SELECT CASE WHEN
  NOT r.rolsuper AND NOT r.rolcreatedb AND NOT r.rolcreaterole AND NOT r.rolbypassrls AND NOT r.rolreplication
  AND NOT EXISTS (SELECT 1 FROM inherited_roles)
  AND NOT EXISTS (SELECT 1 FROM pg_database d WHERE d.datdba = r.oid)
  AND NOT EXISTS (SELECT 1 FROM pg_namespace n WHERE n.nspname = 'public' AND n.nspowner = r.oid)
  AND NOT EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'public' AND c.relowner = r.oid)
  AND NOT EXISTS (SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'public' AND p.proowner = r.oid)
  AND NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname = 'public' AND t.typowner = r.oid)
  AND NOT has_schema_privilege(current_user, 'public', 'CREATE')
  AND NOT has_table_privilege(current_user, 'public."__EFMigrationsHistory"', 'INSERT')
  AND NOT has_table_privilege(current_user, 'public."__EFMigrationsHistory"', 'UPDATE')
  AND NOT has_table_privilege(current_user, 'public."__EFMigrationsHistory"', 'DELETE')
  AND has_table_privilege(current_user, 'public.audit_logs', 'SELECT') AND has_table_privilege(current_user, 'public.audit_logs', 'INSERT')
  AND NOT has_table_privilege(current_user, 'public.audit_logs', 'UPDATE') AND NOT has_table_privilege(current_user, 'public.audit_logs', 'DELETE')
  AND has_table_privilege(current_user, 'public.audit_log_entries', 'SELECT') AND has_table_privilege(current_user, 'public.audit_log_entries', 'INSERT')
  AND NOT has_table_privilege(current_user, 'public.audit_log_entries', 'UPDATE') AND NOT has_table_privilege(current_user, 'public.audit_log_entries', 'DELETE')
  AND has_table_privilege(current_user, 'public.stock_movements', 'SELECT') AND has_table_privilege(current_user, 'public.stock_movements', 'INSERT')
  AND NOT has_table_privilege(current_user, 'public.stock_movements', 'UPDATE') AND NOT has_table_privilege(current_user, 'public.stock_movements', 'DELETE')
  AND has_table_privilege(current_user, 'public.asset_movement_history', 'SELECT') AND has_table_privilege(current_user, 'public.asset_movement_history', 'INSERT')
  AND NOT has_table_privilege(current_user, 'public.asset_movement_history', 'UPDATE') AND NOT has_table_privilege(current_user, 'public.asset_movement_history', 'DELETE')
  AND has_table_privilege(current_user, 'public.document_lifecycle_events', 'SELECT') AND has_table_privilege(current_user, 'public.document_lifecycle_events', 'INSERT')
  AND NOT has_table_privilege(current_user, 'public.document_lifecycle_events', 'UPDATE') AND NOT has_table_privilege(current_user, 'public.document_lifecycle_events', 'DELETE')
  AND has_table_privilege(current_user, 'public.users', 'SELECT') AND has_table_privilege(current_user, 'public.users', 'INSERT') AND has_table_privilege(current_user, 'public.users', 'UPDATE') AND has_table_privilege(current_user, 'public.users', 'DELETE')
  AND has_table_privilege(current_user, 'public.warehouse_documents', 'SELECT') AND has_table_privilege(current_user, 'public.warehouse_documents', 'INSERT') AND has_table_privilege(current_user, 'public.warehouse_documents', 'UPDATE') AND has_table_privilege(current_user, 'public.warehouse_documents', 'DELETE')
  AND has_table_privilege(current_user, 'public.inventory_balances', 'SELECT') AND has_table_privilege(current_user, 'public.inventory_balances', 'INSERT') AND has_table_privilege(current_user, 'public.inventory_balances', 'UPDATE') AND has_table_privilege(current_user, 'public.inventory_balances', 'DELETE')
THEN 'pass' ELSE 'fail' END
FROM pg_roles r WHERE r.rolname = current_user;
SQL
  local psql_succeeded=false
  if [[ -n "$db_root_cert" ]]; then
    if result="$(PGHOST="$normalized_db_host" PGPORT="$db_port" PGDATABASE="$db_name" PGUSER="$db_user" PGPASSWORD="$db_password" PGSSLMODE=verify-full PGSSLROOTCERT="$db_root_cert" PGOPTIONS='-c default_transaction_read_only=on -c statement_timeout=5000' psql -X -Atq -v ON_ERROR_STOP=1 -f "$sql" 2>"$run_dir/psql.stderr")"; then psql_succeeded=true; fi
  else
    if result="$(PGHOST="$normalized_db_host" PGPORT="$db_port" PGDATABASE="$db_name" PGUSER="$db_user" PGPASSWORD="$db_password" PGSSLMODE=verify-full PGOPTIONS='-c default_transaction_read_only=on -c statement_timeout=5000' psql -X -Atq -v ON_ERROR_STOP=1 -f "$sql" 2>"$run_dir/psql.stderr")"; then psql_succeeded=true; fi
  fi
  if [[ "$psql_succeeded" != true ]]; then
    end="$(date +%s%3N)"; record_step database_runtime_role fail unavailable "$((end-start))"; return 1
  fi
  end="$(date +%s%3N)"
  [[ "$result" == pass ]] || { record_step database_runtime_role fail privileges "$((end-start))"; return 1; }
  record_step database_runtime_role pass catalog_acl_only "$((end-start))"
}
run_read_only() { run_http_step health GET /api/v1/health 200 'type=="object" and (.status|type=="string")' || return 1; run_http_step health_live GET /api/v1/health/live 200 'type=="object" and (.status|type=="string")' || return 1; run_http_step health_ready GET /api/v1/health/ready 200 'type=="object" and (.status|type=="string")' || return 1; local p token; for p in /metrics /api/v1/metrics /debug /diagnostics; do run_http_step "closed_${p//\//_}" GET "$p" 404 '.success==false and (.error|type=="object")' || return 1; done; db_preflight || return 1; if [[ -z "$admin_email" || -z "$admin_password" ]]; then record_step administrator_login fail credentials_missing 0; return 1; fi; jq -n '{email:env.EIAMS_STAGING_ADMIN_EMAIL,password:env.EIAMS_STAGING_ADMIN_PASSWORD}' >"$run_dir/login.json"; run_http_step administrator_login POST /api/v1/auth/login 200 '.success==true and (.data.access_token // .data.accessToken | type=="string" and length>20 and length<=4096)' '' "$run_dir/login.json" || return 1; token="$(jq -er '.data.access_token // .data.accessToken | select(type=="string" and test("^[A-Za-z0-9_-]+(\\.[A-Za-z0-9_-]+){2}$") and length<=4096)' "$run_dir/administrator_login.body")" || { record_step administrator_login fail unsafe_token 0; return 1; }; run_http_step authenticated_session GET /api/v1/auth/session 200 '.success==true and (.data|type=="object")' "$token" || return 1; run_http_step administrator_read GET '/api/v1/admin/users?page=1&pageSize=1' 200 '.success==true and (.data|type=="array")' "$token" || return 1; run_http_step dashboard_report GET /api/v1/reports/dashboard 200 '.success==true and (.data|type=="object")' "$token" || return 1; run_http_step inventory_report GET '/api/v1/reports/inventory?page=1&pageSize=1' 200 '.success==true and (.data|type=="array")' "$token" || return 1; printf %s "$token" >"$run_dir/token"; }
run_mutations() { [[ "${!mutation_gate:-}" == 1 ]] || return 0; if [[ "${EIAMS_STAGING_MUTATION_CONFIRMATION:-}" != "$confirmation_phrase" ]]; then record_step mutation_authorization fail confirmation_required 0; return 1; fi; local id="${EIAMS_STAGING_DISPOSABLE_SUBMITTED_ADJUSTMENT_ID:-}" pk="${EIAMS_STAGING_POST_IDEMPOTENCY_KEY:-}" rk="${EIAMS_STAGING_REVERSE_IDEMPOTENCY_KEY:-}" uuid='^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89aAbB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$'; if [[ ! "$id" =~ $uuid || ! "$pk" =~ $uuid || ! "$rk" =~ $uuid || "$pk" == "$rk" ]]; then record_step mutation_authorization fail invalid_identifiers 0; return 1; fi; local token row reversal; token="$(<"$run_dir/token")"; run_http_step disposable_adjustment_before GET "/api/v1/adjustments/$id" 200 '.success==true and (.data.document_status // .data.documentStatus)=="Submitted" and ((.data.row_version // .data.rowVersion)|type=="number" and .>0)' "$token" || return 1; row="$(jq -er '.data.row_version // .data.rowVersion | select(type=="number" and .>0 and floor==.)' "$run_dir/disposable_adjustment_before.body")" || return 1; jq -n --argjson expected_row_version "$row" '{expected_row_version:$expected_row_version}' >"$run_dir/post.json"; printf %s "$pk" >"$run_dir/post-key"; run_http_step adjustment_post POST "/api/v1/adjustments/$id/post" 200 '.success==true and (.data|type=="object")' "$token" "$run_dir/post.json" "$run_dir/post-key" || return 1; run_http_step disposable_adjustment_after_post GET "/api/v1/adjustments/$id" 200 '.success==true and (.data.document_status // .data.documentStatus)=="Posted"' "$token" || return 1; printf %s "$rk" >"$run_dir/reverse-key"; run_http_step adjustment_reverse POST "/api/v1/adjustments/$id/reverse" 201 '.success==true and (.data.id|type=="string" and test("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89aAbB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$"))' "$token" '' "$run_dir/reverse-key" || return 1; reversal="$(jq -er '.data.id | select(type=="string" and test("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89aAbB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$"))' "$run_dir/adjustment_reverse.body")" || { record_step adjustment_reverse fail unsafe_reversal_id 0; return 1; }; run_http_step reversal_detail GET "/api/v1/warehouse-documents/$reversal" 200 '.success==true and (.data|type=="object")' "$token" || return 1; run_http_step dashboard_report_after_mutation GET /api/v1/reports/dashboard 200 '.success==true and (.data|type=="object")' "$token" || return 1; run_http_step inventory_report_after_mutation GET '/api/v1/reports/inventory?page=1&pageSize=1' 200 '.success==true and (.data|type=="array")' "$token" || return 1; }
if ! run_read_only || ! run_mutations; then overall_status=fail; write_evidence; exit 4; fi
write_evidence; echo "Approved staging release evidence completed; sanitized artifact written."
