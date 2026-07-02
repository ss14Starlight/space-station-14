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


def repo_root() -> Path:
    current = Path.cwd().resolve()

    while current != current.parent:
        if (current / "SpaceStation14.slnx").exists() or (current / ".git").exists():
            return current

        current = current.parent

    raise RuntimeError("Could not find repository root.")


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

    return len(text) if end == -1 else end + quote_count


def mask_non_code(text: str) -> str:
    """
    Mask comments, strings, and chars while preserving indexes.
    This prevents commented-out old code from being treated as real code.
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

    if "_Starlight" not in parts:
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


def read_text(path: Path) -> str:
    raw = path.read_bytes()
    encoding = "utf-8-sig" if raw.startswith(b"\xef\xbb\xbf") else "utf-8"

    return raw.decode(encoding, errors="surrogateescape")


def normalize_namespace(raw: str) -> str:
    return re.sub(r"\s+", "", raw)


def line_col(text: str, index: int) -> tuple[int, int]:
    line = 1
    col = 1

    for ch in text[:index]:
        if ch == "\n":
            line += 1
            col = 1
        else:
            col += 1

    return line, col


def escape_action_text(text: str) -> str:
    return (
        text
        .replace("%", "%25")
        .replace("\r", "%0D")
        .replace("\n", "%0A")
    )


def escape_action_property(text: str) -> str:
    return (
        escape_action_text(text)
        .replace(":", "%3A")
        .replace(",", "%2C")
    )


def emit_error(file: str, line: int, col: int, message: str) -> None:
    print(
        f"::error file={escape_action_property(file)},line={line},col={col}::"
        f"{escape_action_text(message)}"
    )


def main() -> int:
    root = repo_root()

    failures = 0
    checked = 0
    skipped_suppressed = 0
    skipped_inactive = 0

    for file in sorted(root.rglob("*.cs")):
        expected = expected_namespace(root, file)
        if expected is None:
            continue

        rel = file.relative_to(root).as_posix()
        text = read_text(file)
        masked = mask_non_code(text)

        namespace_match = NAMESPACE.search(masked)

        if namespace_match is None:
            declaration_match = NEEDS_NAMESPACE.search(masked)

            # Files that only contain comments, disabled old code, or using statements
            # should not be treated as namespace failures.
            if declaration_match is None:
                skipped_inactive += 1
                continue

            if has_namespace_suppression(text, declaration_match.start()):
                skipped_suppressed += 1
                continue

            line, col = line_col(text, declaration_match.start())
            emit_error(rel, line, col, f"No namespace found. Expected '{expected}'.")
            failures += 1
            continue

        if has_namespace_suppression(text, namespace_match.start()):
            skipped_suppressed += 1
            continue

        checked += 1

        current = normalize_namespace(namespace_match.group(1))
        if current == expected:
            continue

        line, col = line_col(text, namespace_match.start(1))
        emit_error(rel, line, col, f"Namespace is '{current}', expected '{expected}'.")
        failures += 1

    if failures == 0:
        print("No _Starlight namespace errors found.")
        print(f"Checked: {checked}")
        print(f"Skipped suppressed: {skipped_suppressed}")
        print(f"Skipped inactive/comment-only: {skipped_inactive}")
        return 0

    print()
    print(f"{failures} _Starlight namespace error(s) found.")
    print(f"Checked: {checked}")
    print(f"Skipped suppressed: {skipped_suppressed}")
    print(f"Skipped inactive/comment-only: {skipped_inactive}")
    print("If a namespace mismatch is intentional, add // ReSharper disable CheckNamespace near the top of the file.")

    return 1


if __name__ == "__main__":
    sys.exit(main())
