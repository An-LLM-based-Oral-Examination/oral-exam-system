import sys
from pathlib import Path
import yaml

workflows_dir = Path(__file__).resolve().parent.parent / ".github" / "workflows"
workflow_files = [
    workflows_dir / "ci.yml",
    workflows_dir / "cd.yml",
]

for wf_path in workflow_files:
    try:
        with open(wf_path, "r", encoding="utf-8") as f:
            data = yaml.safe_load(f)
        assert data is not None, "Parsed YAML is empty"
        assert "jobs" in data, f"No jobs defined in workflow {wf_path.name}"
        print(f"[PASS] Successfully parsed and validated YAML syntax for {wf_path}")
    except Exception as ex:
        print(f"[FAIL] Invalid YAML in {wf_path}: {ex}", file=sys.stderr)
        sys.exit(1)

sys.exit(0)
