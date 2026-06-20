import argparse
import re
import sys
from pathlib import Path

SUPPRESS = re.compile(
    r"\b(?:ReSharper\s+disable(?:\s+once)?|noinspection)\b[^\r\n]*(?:\bCheckNamespace\b|\bAll\b)",
    re.I,
)
RESTORE = re.compile(
    r"\bReSharper\s+restore\b[^\r\n]*(?:\bCheckNamespace\b|\bAll\b)",
    re.I,
)
COMMENT = re.compile(r"//[^\n]*|/\*.*?\*/", re.S)
NAMESPACE = re.compile(
    r"\bnamespace\s+([A-Za-z_@][\w@]*(?:\s*\.\s*[A-Za-z_@][\w@]*)*)"
)
NEEDS_NAMESPACE = re.compile(
    r"\b(?:class|struct|interface|enum|record|delegate)\b"
)
USING_LINE = re.compile(
    r"(?m)^[ \t]*using[ \t]+[^;\r\n]+;[ \t]*(?:\r?\n)?"
)


def repo_root(start: Path) -> Path:
    p = start.resolve()

    if p.is_file():
        p = p.parent

    while p != p.parent:
        if (p / "SpaceStation14.slnx").exists() or (p / ".git").exists():
            return p

        p = p.parent

    raise RuntimeError("Could not find repo root.")


def newline_for(text: str) -> str:
    return "\r\n" if "\r\n" in text else "\n"


def blank(out: list[str], start: int, end: int) -> None:
    for i in range(start, min(end, len(out))):
        if out[i] not in "\r\n":
            out[i] = " "


def raw_string_end(text: str, i: int) -> int | None:
    while i < len(text) and text[i] == "$":
        i += 1

    if i + 2 >= len(text) or text[i:i + 3] != '"""':
        return None

    quote_count = 0
    while i + quote_count < len(text) and text[i + quote_count] == '"':
        quote_count += 1

    if quote_count < 3:
        return None

    delimiter = '"' * quote_count
    end = text.find(delimiter, i + quote_count)

    if end == -1:
        return len(text)

    return end + quote_count


def mask_non_code(text: str) -> str:
    """
    Replaces comments, strings, and chars with spaces while preserving indexes.
    This prevents matching commented-out namespaces/classes.
    """
    out = list(text)
    i = 0

    while i < len(text):
        raw_end = raw_string_end(text, i)
        if raw_end is not None:
            blank(out, i, raw_end)
            i = raw_end
            continue

        if text.startswith("//", i):
            end = text.find("\n", i + 2)
            end = len(text) if end == -1 else end

            blank(out, i, end)
            i = end
            continue

        if text.startswith("/*", i):
            end = text.find("*/", i + 2)
            end = len(text) if end == -1 else end + 2

            blank(out, i, end)
            i = end
            continue

        if text.startswith('@"', i):
            end = i + 2

            while end < len(text):
                if text.startswith('""', end):
                    end += 2
                elif text[end] == '"':
                    end += 1
                    break
                else:
                    end += 1

            blank(out, i, end)
            i = end
            continue

        if text[i] == '"':
            end = i + 1

            while end < len(text):
                if text[end] == "\\":
                    end += 2
                elif text[end] == '"':
                    end += 1
                    break
                else:
                    end += 1

            blank(out, i, end)
            i = end
            continue

        if text[i] == "'":
            end = i + 1

            while end < len(text):
                if text[end] == "\\":
                    end += 2
                elif text[end] == "'":
                    end += 1
                    break
                else:
                    end += 1

            blank(out, i, end)
            i = end
            continue

        i += 1

    return "".join(out)


def expected_namespace(root: Path, file: Path) -> str | None:
    parts = file.relative_to(root).parts

    if "_Starlight" not in parts: # Change _Starlight here if you want to do other files.
        return None

    if not parts[0].startswith("Content."):
        return None

    if parts[-1].endswith((".g.cs", ".g.i.cs")):
        return None

    return ".".join(parts[:-1])


def has_namespace_suppression(text: str, before_index: int) -> bool:
    disabled = False

    for match in COMMENT.finditer(text[:before_index]):
        comment = match.group(0)

        if RESTORE.search(comment):
            disabled = False

        if disabled or SUPPRESS.search(comment):
            return True

    return False


def has_code_needing_namespace(masked: str) -> bool:
    return NEEDS_NAMESPACE.search(masked) is not None


def namespace_insert(text: str, masked: str, expected: str) -> tuple[int, int, str]:
    nl = newline_for(text)
    last_using_end = 0

    for match in USING_LINE.finditer(masked):
        last_using_end = match.end()

    if last_using_end == 0:
        # Namespace at file top, then one blank line before code.
        return 0, 0, f"namespace {expected};{nl}{nl}"

    # Keep exactly:
    # using X;
    #
    # namespace Y;
    #
    # code...
    after_usings = last_using_end
    while after_usings < len(text) and text[after_usings] in " \t\r\n":
        after_usings += 1

    return last_using_end, after_usings, f"{nl}namespace {expected};{nl}{nl}"


def read_text(path: Path) -> tuple[str, str]:
    raw = path.read_bytes()
    encoding = "utf-8-sig" if raw.startswith(b"\xef\xbb\xbf") else "utf-8"

    # surrogateescape preserves invalid UTF-8 bytes exactly on write.
    return raw.decode(encoding, errors="surrogateescape"), encoding


def write_text(path: Path, text: str, encoding: str) -> None:
    path.write_bytes(text.encode(encoding, errors="surrogateescape"))


def normalize_namespace(raw: str) -> str:
    return re.sub(r"\s+", "", raw)


def main() -> int:
    parser = argparse.ArgumentParser(description="Fix _Starlight C# namespaces.")
    parser.add_argument("path", nargs="?", default=".")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    root = repo_root(Path(args.path))

    fixed = 0
    ok = 0
    skipped_suppressed = 0
    skipped_inactive = 0

    for file in sorted(root.rglob("*.cs")):
        expected = expected_namespace(root, file)
        if expected is None:
            continue

        rel = file.relative_to(root).as_posix()
        text, encoding = read_text(file)
        masked = mask_non_code(text)

        match = NAMESPACE.search(masked)

        if match is None:
            if has_namespace_suppression(text, len(text)):
                skipped_suppressed += 1
                continue

            if not has_code_needing_namespace(masked):
                skipped_inactive += 1
                continue

            insert_at, replace_until, insert = namespace_insert(text, masked, expected)

            if args.check:
                print(f"WOULD ADD {rel}: {expected}")
            else:
                text = text[:insert_at] + insert + text[replace_until:]
                write_text(file, text, encoding)
                print(f"ADDED {rel}: {expected}")

            fixed += 1
            continue

        current = normalize_namespace(match.group(1))

        if has_namespace_suppression(text, match.start()):
            skipped_suppressed += 1
            continue

        if current == expected:
            ok += 1
            continue

        if args.check:
            print(f"WOULD FIX {rel}: {current} -> {expected}")
        else:
            text = text[:match.start(1)] + expected + text[match.end(1):]
            write_text(file, text, encoding)
            print(f"FIXED {rel}: {current} -> {expected}")

        fixed += 1

    print()
    print(f"Fixed/added: {fixed}")
    print(f"Already correct: {ok}")
    print(f"Skipped suppressed: {skipped_suppressed}")
    print(f"Skipped inactive/comment-only: {skipped_inactive}")

    return 1 if args.check and fixed else 0


if __name__ == "__main__":
    sys.exit(main())
