#!/usr/bin/env python3

import subprocess
from typing import Iterable

def main() -> int:
    any_failed = False
    for file_name in get_text_files():
        if is_file_crlf(file_name):
            print(f"::error file={file_name},title=File contains CRLF line endings::The file '{file_name}' was committed with CRLF new lines. Please make sure your git client is configured correctly and you are not uploading files directly to GitHub via the web interface.")
            any_failed = True

        trailing = trailing_whitespace_lines(file_name)
        if trailing:
            lines_str = ", ".join(str(l) for l in trailing[:10])
            suffix = f" (and {len(trailing) - 10} more)" if len(trailing) > 10 else ""
            print(f"::error file={file_name},title=Trailing whitespace::The file '{file_name}' has trailing whitespace on line(s): {lines_str}{suffix}. Please remove trailing spaces/tabs.")
            any_failed = True

        if not has_final_newline(file_name):
            print(f"::error file={file_name},title=Missing final newline::The file '{file_name}' does not end with a newline. Please add a final newline.")
            any_failed = True

    return 1 if any_failed else 0


def get_text_files() -> Iterable[str]:
    # https://stackoverflow.com/a/24350112/4678631
    process = subprocess.run(
        ["git", "grep", "--cached", "-Il", ""],
        check=True,
        encoding="utf-8",
        stdout=subprocess.PIPE)

    for x in process.stdout.splitlines():
        yield x.strip()

def is_file_crlf(path: str) -> bool:
    # https://stackoverflow.com/a/29697732/4678631
    with open(path, "rb") as f:
        for line in f:
            if line.endswith(b"\r\n"):
                return True

    return False

def trailing_whitespace_lines(path: str) -> list[int]:
    result = []
    with open(path, "rb") as f:
        for i, line in enumerate(f, start=1):
            stripped = line.rstrip(b"\r\n")
            if stripped != stripped.rstrip(b" \t"):
                result.append(i)
    return result

def has_final_newline(path: str) -> bool:
    with open(path, "rb") as f:
        f.seek(0, 2)
        if f.tell() == 0:
            return True
        f.seek(-1, 2)
        return f.read(1) in (b"\n", b"\r")

exit(main())
