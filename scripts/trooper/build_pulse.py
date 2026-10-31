"""Compatibility entrypoint for the approved Pulse Mk II asset family.
The old simple recipe remains in Git history. Rebuild both matching Pulse assets.
"""
from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).resolve().parents[1]/'pulse_mk2/build.py'),run_name='__main__')
