#!/usr/bin/env bash
set -euo pipefail

# Compatibility entry point. The active RBAC preflight is the dotted-v1
# cutover preflight; this name remains only for operators with old runbooks.
root="$(cd -- "$(dirname -- "$0")/.." && pwd)"
exec "$root/scripts/run-rbac-v2-preflight.sh" "$@"
