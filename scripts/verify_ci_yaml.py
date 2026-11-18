import sys
from pathlib import Path
import yaml

ci_yaml = Path(__file__).resolve().parent.parent / ".github" / "workflows" / "ci.yml"
try:
    with open(ci_yaml, "r", encoding="utf-8") as f:
        data = yaml.safe_load(f)
    assert data is not None, "Parsed YAML is empty"
    assert "jobs" in data, "No jobs defined in CI workflow"
    print(f"[PASS] Successfully parsed and validated YAML syntax for {ci_yaml}")
    sys.exit(0)
except Exception as ex:
    print(f"[FAIL] Invalid YAML in {ci_yaml}: {ex}", file=sys.stderr)
    sys.exit(1)
