#!/bin/sh
set -eu
source_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
mkdir -p "$HOME/.local/bin"
ln -sfn "$source_dir/unity_run.py" "$HOME/.local/bin/unity-run"
printf '%s\n' "Installed $HOME/.local/bin/unity-run"
