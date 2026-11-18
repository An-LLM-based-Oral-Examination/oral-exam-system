#!/usr/bin/env python3
"""
Expand-Contract Migration Linter
--------------------------------
Ensures EF Core migrations follow the Expand-Contract database evolution pattern.
Scans migration files for destructive operations (DropColumn, DropTable, raw SQL drops)
inside the Up() method body while safely ignoring commented-out code and string literals.

Violations can be exempted with an explicit token:
    // [EXPAND_CONTRACT: ALLOW_CONTRACT_DROP]
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import List, Optional, Tuple

EXEMPTION_TOKEN = "[EXPAND_CONTRACT: ALLOW_CONTRACT_DROP]"

# Regex patterns for destructive EF Core operations (matching across newlines and whitespace)
DESTRUCTIVE_METHOD_PATTERNS = [
    ("DropColumn", re.compile(r"\bmigrationBuilder\s*\.\s*DropColumn\s*\(", re.IGNORECASE)),
    ("DropTable", re.compile(r"\bmigrationBuilder\s*\.\s*DropTable\s*\(", re.IGNORECASE)),
]

RAW_SQL_DROP_PATTERNS = [
    (
        "RawSqlDropColumn",
        re.compile(
            r"\bmigrationBuilder\s*\.\s*Sql\s*\(\s*(?:@\$|\$@|@|\$)?\s*[\"'](?:(?!migrationBuilder\s*\.\s*Sql)[\s\S])*?\bDROP\s+COLUMN\b",
            re.IGNORECASE,
        ),
    ),
    (
        "RawSqlDropTable",
        re.compile(
            r"\bmigrationBuilder\s*\.\s*Sql\s*\(\s*(?:@\$|\$@|@|\$)?\s*[\"'](?:(?!migrationBuilder\s*\.\s*Sql)[\s\S])*?\bDROP\s+TABLE\b",
            re.IGNORECASE,
        ),
    ),
]


@dataclass
class MigrationViolation:
    file_path: Path
    line_number: int
    operation: str
    line_content: str


@dataclass
class MigrationExemption:
    file_path: Path
    line_number: int
    operation: str
    line_content: str


def is_migration_file(path: Path) -> bool:
    """Returns True if the file is a C# migration file, excluding designer and snapshot files."""
    name = path.name
    if not name.endswith(".cs"):
        return False
    if name.endswith(".Designer.cs"):
        return False
    if name.endswith("ModelSnapshot.cs"):
        return False
    if name.endswith(".FrozenSchema.cs"):
        return False
    return True


def strip_csharp_lexical(code: str, strip_strings: bool = False, strip_comments: bool = True) -> str:
    """
    Strips comments and/or string literals from C# code while strictly preserving:
    1. Overall length (len(result) == len(code))
    2. Exact character offsets
    3. Newline positions (so line numbers remain 100% identical)
    """
    result: List[str] = []
    i = 0
    n = len(code)

    while i < n:
        # 1. Raw string literals: e.g. \"\"\" or $\"\"\" or $$\"\"\" (C# 11+)
        raw_start = i
        while raw_start < n and code[raw_start] == '$':
            raw_start += 1
        if raw_start + 2 < n and code[raw_start : raw_start + 3] == '"""':
            quote_count = 0
            while raw_start + quote_count < n and code[raw_start + quote_count] == '"':
                quote_count += 1
            end_marker = '"' * quote_count
            content_start = raw_start + quote_count
            end_pos = code.find(end_marker, content_start)
            if end_pos == -1:
                end_idx = n
            else:
                end_idx = end_pos + quote_count

            for k in range(i, end_idx):
                ch = code[k]
                result.append('\n' if ch == '\n' else (' ' if strip_strings else ch))
            i = end_idx
            continue

        # 2. Verbatim string literals: @\"...\" or @$\"...\" or $@\"...\"
        if code[i : i + 2] == '@"' or code[i : i + 3] in ('@$"', '$@"'):
            prefix_len = 3 if code[i : i + 3] in ('@$"', '$@"') else 2
            for k in range(prefix_len):
                result.append(' ' if strip_strings else code[i + k])
            i += prefix_len
            while i < n:
                if code[i] == '"':
                    if i + 1 < n and code[i + 1] == '"':
                        # Escaped quote \"\" in verbatim string
                        result.append('  ' if strip_strings else '""')
                        i += 2
                    else:
                        result.append(' ' if strip_strings else '"')
                        i += 1
                        break
                else:
                    ch = code[i]
                    result.append('\n' if ch == '\n' else (' ' if strip_strings else ch))
                    i += 1
            continue

        # 3. Regular string literals: $\"...\" or \"...\"
        if code[i : i + 2] == '$"' or code[i] == '"':
            prefix_len = 2 if code[i : i + 2] == '$"' else 1
            for k in range(prefix_len):
                result.append(' ' if strip_strings else code[i + k])
            i += prefix_len
            while i < n:
                if code[i] == '\\' and i + 1 < n:
                    if strip_strings:
                        result.append('  ')
                    else:
                        result.append(code[i : i + 2])
                    i += 2
                elif code[i] == '"':
                    result.append(' ' if strip_strings else '"')
                    i += 1
                    break
                elif code[i] == '\n':
                    result.append('\n')
                    i += 1
                    break
                else:
                    result.append(' ' if strip_strings else code[i])
                    i += 1
            continue

        # 4. Character literals: '.'
        if code[i] == "'":
            result.append(' ' if strip_strings else "'")
            i += 1
            while i < n:
                if code[i] == '\\' and i + 1 < n:
                    result.append('  ' if strip_strings else code[i : i + 2])
                    i += 2
                elif code[i] == "'":
                    result.append(' ' if strip_strings else "'")
                    i += 1
                    break
                elif code[i] == '\n':
                    result.append('\n')
                    i += 1
                    break
                else:
                    result.append(' ' if strip_strings else code[i])
                    i += 1
            continue

        # 5. Single-line comments: // until newline
        if code[i : i + 2] == '//':
            while i < n and code[i] != '\n':
                result.append(' ' if strip_comments else code[i])
                i += 1
            continue

        # 6. Multi-line comments: /* until */
        if code[i : i + 2] == '/*':
            i += 2
            result.append('  ' if strip_comments else '/*')
            while i < n:
                if code[i : i + 2] == '*/':
                    result.append('  ' if strip_comments else '*/')
                    i += 2
                    break
                elif code[i] == '\n':
                    result.append('\n')
                    i += 1
                else:
                    result.append(' ' if strip_comments else code[i])
                    i += 1
            continue

        result.append(code[i])
        i += 1

    return "".join(result)


def extract_up_method_span(content: str) -> Optional[Tuple[int, int]]:
    """
    Finds the exact (start_idx, end_idx) of the Up method body in content.
    Supports multiline signatures, block bodies { ... }, and expression bodies => ...;
    Guarantees no premature truncation from braces inside strings or comments.
    """
    clean = strip_csharp_lexical(content, strip_strings=True, strip_comments=True)

    up_pattern = re.compile(
        r"protected\s+override\s+void\s+Up\s*\(\s*(?:\[[^\]]*\]\s*)*MigrationBuilder\s+\w+\s*\)",
        re.MULTILINE,
    )

    match = up_pattern.search(clean)
    if not match:
        return None

    sig_end = match.end()
    n = len(clean)

    idx = sig_end
    while idx < n and clean[idx].isspace():
        idx += 1

    if idx >= n:
        return None

    # Check for expression body => ...;
    if clean[idx : idx + 2] == '=>':
        body_start = idx
        semicolon_pos = clean.find(';', body_start)
        if semicolon_pos == -1:
            return (body_start, n)
        return (body_start, semicolon_pos + 1)

    # Check for block body { ... }
    if clean[idx] == '{':
        body_start = idx
        brace_depth = 0
        for k in range(body_start, n):
            if clean[k] == '{':
                brace_depth += 1
            elif clean[k] == '}':
                brace_depth -= 1
                if brace_depth == 0:
                    return (body_start, k + 1)
        return (body_start, n)

    return None


def extract_up_method_lines(content: str) -> List[Tuple[int, str]]:
    """
    Extracts the line numbers and content of the Up method body from a C# migration.
    Maintained for backward compatibility.
    """
    span = extract_up_method_span(content)
    if not span:
        return []
    start_idx, end_idx = span
    start_line = content[:start_idx].count('\n') + 1
    end_line = content[:end_idx].count('\n') + 1
    lines = content.splitlines()
    result = []
    for l in range(start_line, min(end_line + 1, len(lines) + 1)):
        result.append((l, lines[l - 1]))
    return result


def lint_migration_content(
    file_path: Path, content: str
) -> Tuple[List[MigrationViolation], List[MigrationExemption]]:
    """Lints the text content of a migration file for unexempted destructive operations."""
    violations: List[MigrationViolation] = []
    exemptions: List[MigrationExemption] = []

    span = extract_up_method_span(content)
    if not span:
        return violations, exemptions

    body_start, body_end = span

    # Create code where comments are replaced with whitespace, preserving string literals
    stripped_comments = strip_csharp_lexical(content, strip_strings=False, strip_comments=True)
    up_clean_code = stripped_comments[body_start:body_end]
    up_raw_code = content[body_start:body_end]

    original_lines = content.splitlines()

    # Check method-level or header-level exemption
    body_start_line = content[:body_start].count('\n') + 1
    header_start_line = max(1, body_start_line - 5)
    header_lines = original_lines[header_start_line - 1 : body_start_line]
    header_text = "\n".join(header_lines)

    method_level_exempt = (EXEMPTION_TOKEN in up_raw_code) or (EXEMPTION_TOKEN in header_text)

    all_patterns = DESTRUCTIVE_METHOD_PATTERNS + RAW_SQL_DROP_PATTERNS

    raw_matches = []
    for op_name, pattern in all_patterns:
        for match in pattern.finditer(up_clean_code):
            abs_start = body_start + match.start()
            abs_end = body_start + match.end()
            raw_matches.append((abs_start, abs_end, op_name))

    # Sort matches strictly by source order
    raw_matches.sort(key=lambda item: item[0])

    for abs_start, abs_end, op_name in raw_matches:
        line_num = content[:abs_start].count('\n') + 1
        line_content = original_lines[line_num - 1].strip() if line_num <= len(original_lines) else ""

        is_exempt = method_level_exempt
        if not is_exempt:
            match_start_line = line_num
            match_end_line = content[:abs_end].count('\n') + 1
            for cur_line in range(match_start_line, match_end_line + 1):
                if cur_line <= len(original_lines) and EXEMPTION_TOKEN in original_lines[cur_line - 1]:
                    is_exempt = True
                    break

            if not is_exempt:
                # Check preceding non-empty comment line
                check_line = match_start_line - 1
                while check_line >= 1:
                    prev_text = original_lines[check_line - 1].strip()
                    if not prev_text:
                        check_line -= 1
                        continue
                    if EXEMPTION_TOKEN in prev_text:
                        is_exempt = True
                    break

        if is_exempt:
            exemptions.append(
                MigrationExemption(
                    file_path=file_path,
                    line_number=line_num,
                    operation=op_name,
                    line_content=line_content,
                )
            )
        else:
            violations.append(
                MigrationViolation(
                    file_path=file_path,
                    line_number=line_num,
                    operation=op_name,
                    line_content=line_content,
                )
            )

    return violations, exemptions


def scan_migrations_directory(
    migrations_dir: Path,
) -> Tuple[List[MigrationViolation], List[MigrationExemption], int]:
    """Scans all migration files in the specified directory."""
    violations: List[MigrationViolation] = []
    exemptions: List[MigrationExemption] = []
    scanned_count = 0

    if not migrations_dir.exists():
        print(f"[ERROR] Migrations directory does not exist: {migrations_dir}", file=sys.stderr)
        return violations, exemptions, scanned_count

    for root, _, files in os.walk(migrations_dir):
        for file in sorted(files):
            file_path = Path(root) / file
            if is_migration_file(file_path):
                scanned_count += 1
                try:
                    content = file_path.read_text(encoding="utf-8")
                    v, e = lint_migration_content(file_path, content)
                    violations.extend(v)
                    exemptions.extend(e)
                except Exception as ex:
                    print(f"[WARN] Failed reading {file_path}: {ex}", file=sys.stderr)

    return violations, exemptions, scanned_count


def run_self_tests(repo_root: Optional[Path] = None) -> bool:
    """
    Executes a comprehensive test suite validating all linter rules:
    - multiline Up signature
    - braces in string literals
    - multiline fluent method calls
    - verbatim and multiline raw SQL drops
    - commented-out drops (zero false positives)
    - exemptions (same-line, preceding-line, multiline statement)
    - clean migrations and exclusions
    """
    print("=== Running Expand-Contract Migration Linter Self-Tests ===")
    test_cases = []

    # Test 1: Clean migration
    clean_code = """
    using Microsoft.EntityFrameworkCore.Migrations;
    namespace TestMigrations {
        public partial class CleanMigration : Migration {
            protected override void Up(MigrationBuilder migrationBuilder) {
                migrationBuilder.CreateTable(name: "orders", columns: table => new {});
                migrationBuilder.AddColumn<string>(name: "status", table: "orders");
                migrationBuilder.CreateIndex(name: "ix_orders_status", table: "orders", column: "status");
            }
            protected override void Down(MigrationBuilder migrationBuilder) {
                migrationBuilder.DropTable(name: "orders");
            }
        }
    }
    """
    v, e = lint_migration_content(Path("CleanMigration.cs"), clean_code)
    test_cases.append(("Clean migration Up (DropTable only in Down)", len(v) == 0 and len(e) == 0))

    # Test 2: DropColumn violation in Up
    drop_column_code = """
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropColumn(name: "obsolete_col", table: "users");
    }
    """
    v, e = lint_migration_content(Path("DropColumnMigration.cs"), drop_column_code)
    test_cases.append(("Detect DropColumn in Up", len(v) == 1 and v[0].operation == "DropColumn"))

    # Test 3: DropTable violation in Up
    drop_table_code = """
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropTable(name: "legacy_table");
    }
    """
    v, e = lint_migration_content(Path("DropTableMigration.cs"), drop_table_code)
    test_cases.append(("Detect DropTable in Up", len(v) == 1 and v[0].operation == "DropTable"))

    # Test 4: Preceding line exemption
    exempted_prev_line = """
    protected override void Up(MigrationBuilder migrationBuilder) {
        // [EXPAND_CONTRACT: ALLOW_CONTRACT_DROP]
        migrationBuilder.DropColumn(name: "contract_col", table: "users");
    }
    """
    v, e = lint_migration_content(Path("ExemptedPrevLine.cs"), exempted_prev_line)
    test_cases.append(("Exemption via preceding comment line", len(v) == 0 and len(e) == 1))

    # Test 5: Same line exemption
    exempted_same_line = """
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropTable(name: "contract_table"); // [EXPAND_CONTRACT: ALLOW_CONTRACT_DROP]
    }
    """
    v, e = lint_migration_content(Path("ExemptedSameLine.cs"), exempted_same_line)
    test_cases.append(("Exemption via same-line comment", len(v) == 0 and len(e) == 1))

    # Test 6: Expression body DropColumn
    expression_body_code = """
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn("col", "tbl");
    """
    v, e = lint_migration_content(Path("ExpressionBody.cs"), expression_body_code)
    test_cases.append(("Detect DropColumn in expression body Up", len(v) == 1))

    # Test 7: Raw SQL DROP COLUMN detection
    raw_sql_code = """
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.Sql("ALTER TABLE users DROP COLUMN legacy_field;");
    }
    """
    v, e = lint_migration_content(Path("RawSqlDrop.cs"), raw_sql_code)
    test_cases.append(("Detect raw SQL DROP COLUMN in Up", len(v) == 1 and v[0].operation == "RawSqlDropColumn"))

    # Test 8: Multiline Up method signature
    multiline_sig_code = """
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "split_sig_col", table: "users");
    }
    """
    v, e = lint_migration_content(Path("MultilineSig.cs"), multiline_sig_code)
    test_cases.append(("Support multiline Up signature", len(v) == 1 and v[0].operation == "DropColumn"))

    # Test 9: Braces inside string literals do not truncate Up()
    braces_in_strings_code = """
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE FUNCTION fn() { RAISE NOTICE 'brace: }'; }");
        migrationBuilder.DropColumn(name: "post_brace_col", table: "users");
    }
    """
    v, e = lint_migration_content(Path("BracesInStrings.cs"), braces_in_strings_code)
    test_cases.append(("Braces in string literals do not truncate Up()", len(v) == 1 and v[0].operation == "DropColumn"))

    # Test 10: Multiline fluent method calls across newlines
    multiline_fluent_code = """
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder
            .DropColumn(
                name: "multiline_col",
                table: "users");
    }
    """
    v, e = lint_migration_content(Path("MultilineFluent.cs"), multiline_fluent_code)
    test_cases.append(("Detect multiline fluent DropColumn call", len(v) == 1 and v[0].operation == "DropColumn"))

    # Test 11: Verbatim and multiline raw SQL drops
    verbatim_multiline_sql_code = """
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE users
            DROP COLUMN legacy_col;
        ");
    }
    """
    v, e = lint_migration_content(Path("VerbatimMultilineSql.cs"), verbatim_multiline_sql_code)
    test_cases.append(("Detect verbatim multiline raw SQL DROP COLUMN", len(v) == 1 and v[0].operation == "RawSqlDropColumn"))

    # Test 12: Commented-out drops (single-line and multi-line comments) are ignored
    commented_out_code = """
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // migrationBuilder.DropColumn(name: "commented_col", table: "users");
        /*
        migrationBuilder.DropTable(name: "commented_table");
        migrationBuilder.Sql("ALTER TABLE users DROP COLUMN comment_sql;");
        */
        migrationBuilder.AddColumn<string>(name: "new_col", table: "users");
    }
    """
    v, e = lint_migration_content(Path("CommentedOut.cs"), commented_out_code)
    test_cases.append(("Eliminate false positives from comments", len(v) == 0 and len(e) == 0))

    # Test 13: Exemption token on multiline fluent statement
    multiline_exempted_code = """
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // [EXPAND_CONTRACT: ALLOW_CONTRACT_DROP]
        migrationBuilder
            .DropTable(
                name: "legacy_table");
    }
    """
    v, e = lint_migration_content(Path("MultilineExempted.cs"), multiline_exempted_code)
    test_cases.append(("Exemption token on multiline fluent DropTable", len(v) == 0 and len(e) == 1))

    # Test 14: File exclusion tests
    test_cases.append(("Exclude Designer.cs", not is_migration_file(Path("20261114_Test.Designer.cs"))))
    test_cases.append(("Exclude ModelSnapshot.cs", not is_migration_file(Path("OralExamDbContextModelSnapshot.cs"))))
    test_cases.append(("Include valid migration .cs", is_migration_file(Path("20261114_Test.cs"))))

    # Test 15: Run tests against real codebase if directory exists
    if repo_root is not None:
        real_migrations = repo_root / "backend" / "src" / "Infrastructure" / "Migrations"
        if real_migrations.exists():
            rv, re_list, count = scan_migrations_directory(real_migrations)
            test_cases.append((f"Real codebase scan ({count} files in {real_migrations})", len(rv) == 0))

    all_passed = True
    for name, passed in test_cases:
        status = "[PASS]" if passed else "[FAIL]"
        print(f"  {status} {name}")
        if not passed:
            all_passed = False

    if all_passed:
        print(f"[TEST SUCCESS] All {len(test_cases)} self-tests passed successfully.")
    else:
        print("[TEST FAILURE] One or more self-tests failed.", file=sys.stderr)

    return all_passed


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Lints EF Core migrations for Expand-Contract architectural compliance."
    )
    parser.add_argument(
        "--path",
        type=Path,
        default=None,
        help="Path to the migrations directory (default: backend/src/Infrastructure/Migrations).",
    )
    parser.add_argument(
        "--test",
        action="store_true",
        help="Run self-verification test suite.",
    )
    args = parser.parse_args()

    script_dir = Path(__file__).resolve().parent
    repo_root = script_dir.parent

    if args.test:
        success = run_self_tests(repo_root=repo_root)
        return 0 if success else 1

    migrations_dir = args.path
    if migrations_dir is None:
        migrations_dir = repo_root / "backend" / "src" / "Infrastructure" / "Migrations"
        if not migrations_dir.exists():
            migrations_dir = Path("backend/src/Infrastructure/Migrations")

    print("=== Expand-Contract Database Migration Linter ===")
    print(f"Target directory: {migrations_dir.resolve()}")

    violations, exemptions, count = scan_migrations_directory(migrations_dir)

    print(f"Scanned {count} migration files.")
    if exemptions:
        print(f"Detected {len(exemptions)} explicitly exempted contract drops:")
        for e in exemptions:
            print(f"  [EXEMPTED] {e.file_path.name}:{e.line_number} -> {e.operation}: {e.line_content}")

    if violations:
        print("\n[VIOLATIONS DETECTED]")
        print("Destructive schema operations (DropColumn/DropTable) are forbidden during the Expand phase.")
        for v in violations:
            print(f"  [FAIL] {v.file_path}:{v.line_number} -> {v.operation}")
            print(f"         {v.line_content}")
        print("\nResolution:")
        print("  1. In the Expand phase, deprecate the column/table in code without dropping it from the database.")
        print(f"  2. If performing a planned Contract phase drop, annotate the operation with:")
        print(f"     // {EXEMPTION_TOKEN}")
        return 1

    print(f"[SUCCESS] All {count} migrations comply with the Expand-Contract pattern (0 destructive drops).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
