#!/usr/bin/env python3
"""Fail if tracked text contains emoji, em dashes, or en dashes.

Skips third-party libraries and EF migration snapshots. Migrations are generated
and are not rewritten by hand.
"""

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

SKIP_DIR_PARTS = (
    "/wwwroot/lib/",
    "/Migrations/",
)

TEXT_EXTENSIONS = {
    ".cs", ".cshtml", ".js", ".css", ".py", ".md", ".yml", ".yaml", ".json",
    ".xml", ".txt", ".html", ".ps1", ".bat", ".sh", ".csproj", ".sln",
    ".editorconfig", ".gitignore", ".gitattributes", ".example", ".env",
    ".razor", ".sql", ".http", ".props", ".targets",
}

# Emoji, dingbats, and the extra symbols that showed up in logs and docs.
EMOJI_RANGES = (
    (0x1F300, 0x1FAFF),
    (0x2600, 0x27BF),
    (0x2B50, 0x2B50),
    (0x231A, 0x231B),
    (0x23E9, 0x23F3),
    (0x23F8, 0x23FA),
    (0x25AA, 0x25FE),
    (0x2934, 0x2935),
    (0x2B05, 0x2B07),
    (0x2B1B, 0x2B1C),
    (0x3030, 0x3030),
    (0x303D, 0x303D),
    (0x3297, 0x3299),
    (0xFE0F, 0xFE0F),
    (0x200D, 0x200D),
)

DASHES = ("\u2014", "\u2013")


def is_emoji(code: int) -> bool:
    return any(start <= code <= end for start, end in EMOJI_RANGES)


ALWAYS_TEXT_NAMES = {
    ".gitignore",
    ".gitattributes",
    ".env.example",
    ".dockerignore",
    "Dockerfile",
    "LICENSE",
    "SECURITY.md",
}


def tracked_files() -> list[Path]:
    result = subprocess.run(
        ["git", "ls-files", "-z"],
        cwd=ROOT,
        check=True,
        capture_output=True,
    )
    paths: list[Path] = []
    for raw in result.stdout.split(b"\0"):
        if not raw:
            continue
        rel = raw.decode("utf-8", errors="surrogateescape").replace("\\", "/")
        lowered = "/" + rel
        if "/wwwroot/lib/" in lowered or "/Migrations/" in lowered:
            continue
        path = ROOT / rel
        if path.suffix.lower() not in TEXT_EXTENSIONS and path.name not in ALWAYS_TEXT_NAMES:
            continue
        paths.append(path)
    return paths


def clean_text(text: str) -> str:
    for dash in DASHES:
        text = text.replace(dash, "-")
    return "".join(ch for ch in text if not is_emoji(ord(ch)))


def problems_in(text: str) -> list[tuple[int, str]]:
    found: list[tuple[int, str]] = []
    for lineno, line in enumerate(text.splitlines(), start=1):
        reasons = []
        if any(dash in line for dash in DASHES):
            reasons.append("dash")
        if any(is_emoji(ord(ch)) for ch in line):
            reasons.append("emoji")
        if reasons:
            found.append((lineno, ",".join(reasons)))
    return found


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--fix", action="store_true", help="Rewrite files in place")
    args = parser.parse_args()

    failures = 0
    for path in tracked_files():
        if not path.is_file():
            continue
        raw = path.read_bytes()
        original = raw.decode("utf-8", errors="replace")
        hits = problems_in(original)
        if not hits:
            continue
        if args.fix:
            cleaned = clean_text(original)
            path.write_bytes(cleaned.encode("utf-8"))
            print(f"fixed {path.relative_to(ROOT)} ({len(hits)} lines)")
            continue
        failures += 1
        rel = path.relative_to(ROOT)
        for lineno, reason in hits[:8]:
            print(f"{rel}:{lineno}: {reason}")
        if len(hits) > 8:
            print(f"{rel}: ... {len(hits) - 8} more")

    if failures and not args.fix:
        print(f"\n{failures} files contain emoji or disallowed dashes.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
