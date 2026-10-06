#!/usr/bin/env bash
set -euo pipefail
TROOPER_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$TROOPER_ROOT"
TROOPER_BLENDER="${TROOPER_BLENDER:-blender}"
# Requires the recovered existing v2 from run.sh. Source and v2 stay untouched.
"$TROOPER_BLENDER" --background --python-exit-code 1 --python scripts/trooper/unity_grip_bake.py
"$TROOPER_BLENDER" --background --python-exit-code 1 --python scripts/trooper/unity_fit_contact.py
"$TROOPER_BLENDER" --background --python-exit-code 1 --python scripts/trooper/unity_grip_bake.py -- --fitted
"$TROOPER_BLENDER" --background --python-exit-code 1 --python scripts/trooper/unity_build_shipping.py
"$TROOPER_BLENDER" --background --python-exit-code 1 --python scripts/trooper/unity_audit_shipping.py
"$TROOPER_BLENDER" --background --python-exit-code 1 --python scripts/trooper/unity_deformation.py
