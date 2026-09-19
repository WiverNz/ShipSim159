"""Summarize Unity XML results from this checkout, without executing project code."""
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

if len(sys.argv) != 1:
    raise SystemExit("No arguments accepted")
root = (Path(__file__).resolve().parents[1] / "TestResults").resolve()
for path in sorted(root.glob("*.xml")):
    if path.is_symlink() or path.resolve().parent != root:
        continue
    try:
        result = ET.parse(path).getroot()
        print(json.dumps({"file": path.name, **{key: result.get(key) for key in
            ("result", "total", "passed", "failed", "start-time", "end-time")}}, ensure_ascii=False))
    except (OSError, ET.ParseError) as error:
        print(json.dumps({"file": path.name, "error": str(error)}))
