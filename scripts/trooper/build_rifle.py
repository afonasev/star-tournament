"""Compatibility entry point for the current detailed COBALT authoring source.

The pre-2026-10-26 prototype remains reproducible from Git efc73a7a.
See build_cobalt.py; all arguments are forwarded, including -- --preview-only.
"""
import runpy
from pathlib import Path

runpy.run_path(str(Path(__file__).with_name('build_cobalt.py')), run_name='__main__')
