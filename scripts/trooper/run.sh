#!/usr/bin/env bash
set -euo pipefail
TROOPER_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$TROOPER_ROOT"
TROOPER_BLENDER="${TROOPER_BLENDER:-blender}"
TROOPER_PYTHON="${TROOPER_PYTHON:-python3}"
"$TROOPER_PYTHON" -c 'from PIL import Image' # Fail before authoring if contact-sheet dependency is missing.
"$TROOPER_BLENDER" --background --python-exit-code 1 --python scripts/trooper/geometry_checks.py
"$TROOPER_BLENDER" --background --python-exit-code 1 --python scripts/trooper/inspect_source.py
"$TROOPER_BLENDER" --background --python-exit-code 1 --python scripts/trooper/build.py
"$TROOPER_BLENDER" --background --python-exit-code 1 --python scripts/trooper/audit.py
"$TROOPER_BLENDER" --background --python-exit-code 1 --python scripts/trooper/review_rig.py
"$TROOPER_BLENDER" --background --python-exit-code 1 --python scripts/trooper/review_hands.py
"$TROOPER_PYTHON" scripts/trooper/contact_sheets.py
"$TROOPER_PYTHON" scripts/trooper/finalize.py
