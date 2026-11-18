#!/usr/bin/env python3
"""
Unit tests for Expand-Contract Migration Linter.
"""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

# Ensure repo root is on sys.path
_SCRIPT_DIR = Path(__file__).resolve().parent
_REPO_ROOT = _SCRIPT_DIR.parent
if str(_REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(_REPO_ROOT))

from scripts.lint_migrations_expand_contract import run_self_tests, scan_migrations_directory


class TestExpandContractMigrationLinter(unittest.TestCase):
    def test_all_linter_self_tests(self):
        success = run_self_tests(repo_root=_REPO_ROOT)
        self.assertTrue(success, "Expand-contract migration linter self-tests failed")

    def test_real_codebase_migrations_have_zero_violations(self):
        migrations_dir = _REPO_ROOT / "backend" / "src" / "Infrastructure" / "Migrations"
        violations, exemptions, count = scan_migrations_directory(migrations_dir)
        self.assertGreater(count, 0, "No migration files found to scan")
        self.assertEqual(len(violations), 0, f"Found {len(violations)} migration violations")


if __name__ == "__main__":
    unittest.main()
