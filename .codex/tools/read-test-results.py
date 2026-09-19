"""Compatibility entry point for existing machine-specific approvals."""
import runpy
import sys

if len(sys.argv) != 1:
    raise SystemExit("No arguments accepted")
runpy.run_path("/mnt/g/projects/shipsim159/Tools/read-test-results.py", run_name="__main__")
