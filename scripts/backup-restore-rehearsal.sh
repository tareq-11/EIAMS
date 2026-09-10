#!/usr/bin/env bash
# A deliberately narrow recovery rehearsal.  It is not a backup scheduler and is never enabled
# by application configuration.  It accepts only the disposable integration Testcontainers DB.
set -euo pipefail

readonly gate="RUN_LOCAL_BACKUP_RESTORE_REHEARSAL"
readonly source_url="${EIAMS_REHEARSAL_SOURCE_DATABASE_URL:-}"
readonly admin_url="${EIAMS_REHEARSAL_ADMIN_DATABASE_URL:-}"
readonly source_root="${EIAMS_REHEARSAL_ATTACHMENT_ROOT:-}"
readonly evidence_dir="${EIAMS_REHEARSAL_EVIDENCE_DIR:-}"
readonly rpo_objective="${EIAMS_REHEARSAL_RPO_OBJECTIVE_SECONDS:-}"
readonly rto_objective="${EIAMS_REHEARSAL_RTO_OBJECTIVE_SECONDS:-}"

refuse() { echo "Refusing backup/restore rehearsal: $1" >&2; exit 3; }

[[ "${!gate:-}" == "1" ]] || refuse "set $gate=1 for a disposable local Testcontainers run"
[[ -n "$source_url" && -n "$admin_url" && -n "$source_root" && -n "$evidence_dir" ]] || \
  refuse "source/admin URLs, attachment root, and evidence directory are required environment values"
[[ "$rpo_objective" =~ ^[1-9][0-9]*$ && "$rto_objective" =~ ^[1-9][0-9]*$ ]] || \
  refuse "RPO and RTO objectives must be positive whole seconds"

for tool in psql pg_dump pg_restore createdb dropdb sha256sum tar find realpath stat awk cp; do
  command -v "$tool" >/dev/null 2>&1 || refuse "required tool '$tool' is unavailable"
done

load_loopback_uri() {
  # URI-only and deliberately restrictive. This lets us pass credentials via libpq environment,
  # never in a child-process command line; encoded or unusual URLs fail closed.
  [[ "$2" =~ ^postgres(ql)?://([^:/@%]+):([^@/%]+)@(localhost|127\.0\.0\.1|\[::1\])(:([0-9]+))?/([^/?]+)$ ]] || return 1
  local prefix="$1"
  printf -v "${prefix}_PGUSER" '%s' "${BASH_REMATCH[2]}"
  printf -v "${prefix}_PGPASSWORD" '%s' "${BASH_REMATCH[3]}"
  printf -v "${prefix}_PGHOST" '%s' "${BASH_REMATCH[4]}"
  printf -v "${prefix}_PGPORT" '%s' "${BASH_REMATCH[6]:-5432}"
  printf -v "${prefix}_PGDATABASE" '%s' "${BASH_REMATCH[7]}"
}

load_loopback_uri SOURCE "$source_url" || refuse "source must be a simple credentialed loopback PostgreSQL URI"
load_loopback_uri ADMIN "$admin_url" || refuse "admin target must be a simple credentialed loopback PostgreSQL URI"
[[ "${source_url,,}" != *neon* && "${admin_url,,}" != *neon* ]] || refuse "Neon is never permitted"

psql_source() { env PGPASSWORD="$SOURCE_PGPASSWORD" PGHOST="$SOURCE_PGHOST" PGPORT="$SOURCE_PGPORT" PGUSER="$SOURCE_PGUSER" PGDATABASE="$SOURCE_PGDATABASE" psql "$@"; }
psql_target() { env PGPASSWORD="$ADMIN_PGPASSWORD" PGHOST="$ADMIN_PGHOST" PGPORT="$ADMIN_PGPORT" PGUSER="$ADMIN_PGUSER" PGDATABASE="$target_db" psql "$@"; }
source_db="$(psql_source -X -Atq -v ON_ERROR_STOP=1 -c 'SELECT current_database()')"
admin_db="$(env PGPASSWORD="$ADMIN_PGPASSWORD" PGHOST="$ADMIN_PGHOST" PGPORT="$ADMIN_PGPORT" PGUSER="$ADMIN_PGUSER" PGDATABASE="$ADMIN_PGDATABASE" psql -X -Atq -v ON_ERROR_STOP=1 -c 'SELECT current_database()')"
[[ "$source_db" == "clean_architecture_integration_test" ]] || refuse "source is not the disposable integration database"
[[ "$admin_db" == "postgres" ]] || refuse "admin URL must connect to the postgres maintenance database"

temp_base="$(realpath -e "${TMPDIR:-/tmp}")"
source_root_real="$(realpath -e "$source_root")" || refuse "attachment root does not exist"
[[ "$source_root_real" == "$temp_base"/eiams-* ]] || refuse "attachment root must be a disposable temp eiams-* directory"
mkdir -p "$evidence_dir"
evidence_real="$(realpath -e "$evidence_dir")"
[[ "$evidence_real" == "$temp_base"/* ]] || refuse "evidence directory must be below the system temp directory"

run_dir="$(mktemp -d "$temp_base/eiams-backup-restore.XXXXXX")"
target_db="clean_architecture_restore_$(date -u +%s)_$RANDOM"
target_created=0
cleanup() {
  local result=$?
  if [[ "$target_created" == "1" ]]; then
    env PGPASSWORD="$ADMIN_PGPASSWORD" PGHOST="$ADMIN_PGHOST" PGPORT="$ADMIN_PGPORT" PGUSER="$ADMIN_PGUSER" PGDATABASE="$ADMIN_PGDATABASE" dropdb --if-exists --force "$target_db" >/dev/null 2>&1 || \
      echo "WARNING: disposable restore database cleanup failed; manually remove the generated local Testcontainers target." >&2
  fi
  rm -rf -- "$run_dir"
  exit "$result"
}
trap cleanup EXIT

assert_source_quiescent() {
  local sessions
  sessions="$(psql_source -X -Atq -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid()")"
  [[ "$sessions" == "0" ]] || refuse "source is not quiesced; close every other source database session"
}

capture_manifest() {
  psql_source -X -Atq -v ON_ERROR_STOP=1 -F $'\t' -c "SELECT (SELECT count(*) FROM public.users), (SELECT count(*) FROM public.audit_logs), (SELECT count(*) FROM public.document_attachments), COALESCE((SELECT md5(string_agg(storage_key || ':' || checksum || ':' || file_size::text, '|' ORDER BY storage_key)) FROM public.document_attachments), md5(''))"
}

# This is a quiesced maintenance-window rehearsal, not an online atomic backup algorithm.
assert_source_quiescent
recovery_point_epoch="$(psql_source -X -Atq -v ON_ERROR_STOP=1 -c 'SELECT floor(extract(epoch FROM clock_timestamp()))::bigint')"
manifest_before="$(capture_manifest)"

# The script intentionally does not emit keys, IDs, URLs, SQL, or individual checksums.
IFS=$'\t' read -r user_count audit_count attachment_count attachment_manifest_hash <<<"$manifest_before"
[[ "$attachment_count" =~ ^[1-9][0-9]*$ ]] || refuse "source needs at least one attachment for a consistency rehearsal"
[[ "$user_count" =~ ^[1-9][0-9]*$ ]] || refuse "source needs representative database data"

backup_files_dir="$run_dir/backup-files"
restored_files_dir="$run_dir/restored-files"
mkdir -p "$backup_files_dir" "$restored_files_dir"
while IFS=$'\t' read -r storage_key checksum file_size; do
  [[ "$storage_key" =~ ^[0-9a-f]{32}$ && "$checksum" =~ ^[0-9a-f]{64}$ && "$file_size" =~ ^[1-9][0-9]*$ ]] || \
    refuse "attachment metadata violates the local-storage integrity contract"
  source_file="$source_root_real/$storage_key"
  [[ -f "$source_file" && ! -L "$source_file" ]] || refuse "attachment metadata has missing or unsafe content"
  [[ "$(stat -c '%s' "$source_file")" == "$file_size" ]] || refuse "attachment size mismatch"
  [[ "$(sha256sum "$source_file" | awk '{print $1}')" == "$checksum" ]] || refuse "attachment checksum mismatch"
  cp -- "$source_file" "$backup_files_dir/$storage_key"
done < <(psql_source -X -Atq -v ON_ERROR_STOP=1 -F $'\t' -c \
  'SELECT storage_key, checksum, file_size FROM public.document_attachments ORDER BY storage_key')

source_file_count="$(find "$source_root_real" -maxdepth 1 -type f -not -lname '*' | wc -l | tr -d ' ')"
[[ "$source_file_count" == "$attachment_count" ]] || refuse "source attachment root has orphan or untracked files"

dump_file="$run_dir/database.dump"
env PGPASSWORD="$SOURCE_PGPASSWORD" PGHOST="$SOURCE_PGHOST" PGPORT="$SOURCE_PGPORT" PGUSER="$SOURCE_PGUSER" PGDATABASE="$SOURCE_PGDATABASE" pg_dump --format=custom --no-owner --no-privileges --file="$dump_file"
database_sha="$(sha256sum "$dump_file" | awk '{print $1}')"
files_sha="$(tar --sort=name --mtime='UTC 1970-01-01' --owner=0 --group=0 --numeric-owner -cf - -C "$backup_files_dir" . | sha256sum | awk '{print $1}')"

assert_source_quiescent
manifest_after="$(capture_manifest)"
[[ "$manifest_after" == "$manifest_before" ]] || refuse "source metadata changed during the non-atomic backup window"
source_file_count_after="$(find "$source_root_real" -maxdepth 1 -type f -not -lname '*' | wc -l | tr -d ' ')"
source_files_sha_after="$(tar --sort=name --mtime='UTC 1970-01-01' --owner=0 --group=0 --numeric-owner -cf - -C "$source_root_real" . | sha256sum | awk '{print $1}')"
[[ "$source_file_count_after" == "$attachment_count" && "$source_files_sha_after" == "$files_sha" ]] || refuse "source attachment content changed during the non-atomic backup window"
# The coherent backup set is now finalized. The simulated incident begins after this point.
incident_epoch="$(date -u +%s)"

env PGPASSWORD="$ADMIN_PGPASSWORD" PGHOST="$ADMIN_PGHOST" PGPORT="$ADMIN_PGPORT" PGUSER="$ADMIN_PGUSER" PGDATABASE="$ADMIN_PGDATABASE" createdb "$target_db"
target_created=1
restore_started_epoch="$incident_epoch"
env PGPASSWORD="$ADMIN_PGPASSWORD" PGHOST="$ADMIN_PGHOST" PGPORT="$ADMIN_PGPORT" PGUSER="$ADMIN_PGUSER" PGDATABASE="$target_db" pg_restore --dbname="$target_db" --exit-on-error --no-owner --no-privileges "$dump_file"
# Restore attachment content into a distinct disposable storage root, rather than validating the
# backup copy in place. Its path is intentionally never persisted in evidence.
cp -- "$backup_files_dir"/* "$restored_files_dir"/
restore_finished_epoch="$(date -u +%s)"

for table_and_expected in "users:$user_count" "audit_logs:$audit_count" "document_attachments:$attachment_count"; do
  table="${table_and_expected%%:*}"; expected="${table_and_expected#*:}"
  actual="$(psql_target -X -Atq -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM public.$table")"
  [[ "$actual" == "$expected" ]] || refuse "restored representative row count differs"
done

while IFS=$'\t' read -r storage_key checksum file_size; do
  restored_file="$restored_files_dir/$storage_key"
  [[ -f "$restored_file" && "$(stat -c '%s' "$restored_file")" == "$file_size" ]] || refuse "restored attachment content is missing"
  [[ "$(sha256sum "$restored_file" | awk '{print $1}')" == "$checksum" ]] || refuse "restored attachment checksum differs"
done < <(psql_target -X -Atq -v ON_ERROR_STOP=1 -F $'\t' -c \
  'SELECT storage_key, checksum, file_size FROM public.document_attachments ORDER BY storage_key')

restored_file_count="$(find "$restored_files_dir" -maxdepth 1 -type f -not -lname '*' | wc -l | tr -d ' ')"
[[ "$restored_file_count" == "$attachment_count" ]] || refuse "restored attachment set contains orphan files"

finished_epoch="$(date -u +%s)"
# Potential data loss is only the recovery-point-to-simulated-incident gap. RTO spans the
# simulated incident through DB/file restore and completed verification.
potential_data_loss_seconds=$(( incident_epoch - recovery_point_epoch ))
rto_seconds=$(( finished_epoch - incident_epoch ))
rpo_status="pass"; [[ "$potential_data_loss_seconds" -le "$rpo_objective" ]] || rpo_status="fail"
rto_status="pass"; [[ "$rto_seconds" -le "$rto_objective" ]] || rto_status="fail"
overall_status="pass"; [[ "$rpo_status" == pass && "$rto_status" == pass ]] || overall_status="fail"

evidence_file="$evidence_real/backup-restore-rehearsal-$(date -u +%Y%m%dT%H%M%SZ).json"
umask 077
printf '{\n  "scope":"local Testcontainers disposable integration database only; quiesced maintenance-window rehearsal, not online atomic backup",\n  "status":"%s",\n  "manifest":{"databaseSha256":"%s","attachmentSetSha256":"%s","stableBeforeAndAfterCapture":true},\n  "verification":{"representativeUserRows":%s,"auditRows":%s,"attachmentRows":%s,"missingFiles":0,"orphanFiles":0,"checksumMismatches":0},\n  "objectives":{"maximumPotentialDataLossSeconds":%s,"maximumRecoveryTimeSeconds":%s},\n  "measurements":{"potentialDataLossSeconds":%s,"recoveryTimeSeconds":%s,"potentialDataLossStatus":"%s","recoveryTimeStatus":"%s"},\n  "limits":"Potential data loss is the recovery-point marker to simulated incident after the coherent local backup set finalized; it is not production backup-age validation. Recovery time spans simulated incident through verified DB and file restore. No production, Neon, secrets, identifiers, storage keys, or connection strings were recorded.",\n  "retention":"Audit logs remain append-only with no automatic deletion. This rehearsal changes no refresh-token retention or cleanup policy; no business retention value is inferred."\n}\n' \
  "$overall_status" "$database_sha" "$files_sha" "$user_count" "$audit_count" "$attachment_count" \
  "$rpo_objective" "$rto_objective" "$potential_data_loss_seconds" "$rto_seconds" "$rpo_status" "$rto_status" >"$evidence_file"

[[ "$overall_status" == pass ]] || { echo "Rehearsal completed but missed an explicit local objective; see redacted evidence artifact." >&2; exit 4; }
echo "Local backup/restore rehearsal passed; redacted evidence artifact written."
