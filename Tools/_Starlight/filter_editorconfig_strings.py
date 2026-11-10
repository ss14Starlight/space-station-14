#!/usr/bin/env python3


import re
import sys

_REPORT_LINE = re.compile(r"^(?P<file>.+?):(?P<line>\d+):(?P<col>\d+): \w+: (?P<msg>.*)$")
_INDENT_MSG = re.compile(r"left-padding|indentation type", re.IGNORECASE)
_RAW_START = re.compile(r'(\$*)("{3,})')
_VERBATIM_START = re.compile(r'(\$@|@\$|@)"')
_REGULAR_START = re.compile(r'(\$?)"')


def string_lines(text):
    result = set()
    # Frames: ["code", depth, is_hole], ["line_comment"], ["block_comment"],
    # ["str", interp], ["verbatim", interp], ["raw", quotes, dollars]
    stack = [["code", 0, False]]
    i, n, line = 0, len(text), 1

    while i < n:
        c = text[i]
        top = stack[-1]
        kind = top[0]

        if c == "\n":
            line += 1
            i += 1
            if kind in ("verbatim", "raw"):
                result.add(line)
            elif kind == "line_comment":
                stack.pop()
            continue

        if kind == "code":
            if text.startswith("//", i):
                stack.append(["line_comment"])
                i += 2
                continue
            if text.startswith("/*", i):
                stack.append(["block_comment"])
                i += 2
                continue
            if c == "'":
                # Char literal: skip to the closing quote.
                j = i + 1
                while j < n and text[j] not in "'\n":
                    j += 2 if text[j] == "\\" else 1
                i = j + 1
                continue
            if c in '$@"':
                m = _RAW_START.match(text, i)
                if m:
                    stack.append(["raw", len(m.group(2)), len(m.group(1))])
                    i = m.end()
                    continue
                m = _VERBATIM_START.match(text, i)
                if m:
                    stack.append(["verbatim", "$" in m.group(1)])
                    i = m.end()
                    continue
                m = _REGULAR_START.match(text, i)
                if m:
                    stack.append(["str", bool(m.group(1))])
                    i = m.end()
                    continue
            if top[2]:
                if c == "{":
                    top[1] += 1
                elif c == "}":
                    if top[1] == 0:
                        stack.pop()
                    else:
                        top[1] -= 1
            i += 1
            continue

        if kind == "line_comment":
            i += 1
            continue

        if kind == "block_comment":
            if text.startswith("*/", i):
                stack.pop()
                i += 2
            else:
                i += 1
            continue

        if kind == "raw":
            quotes, dollars = top[1], top[2]
            if text.startswith('"' * quotes, i):
                stack.pop()
                i += quotes
                continue
            if dollars and text.startswith("{" * dollars, i):
                stack.append(["code", 0, True])
                i += dollars
                continue
            i += 1
            continue

        # Regular or verbatim string.
        interp = top[1]
        if kind == "str" and c == "\\":
            i += 2
            continue
        if c == '"':
            if kind == "verbatim" and text.startswith('""', i):
                i += 2
                continue
            stack.pop()
            i += 1
            continue
        if interp and c == "{":
            if text.startswith("{{", i):
                i += 2
                continue
            stack.append(["code", 0, True])
        i += 1

    return result


def main():
    if len(sys.argv) != 2:
        print(__doc__, file=sys.stderr)
        return 1

    report_path = sys.argv[1]
    with open(report_path, encoding="utf-8", errors="replace") as f:
        report = f.read().splitlines()

    cache = {}
    kept = []
    dropped = 0
    for entry in report:
        m = _REPORT_LINE.match(entry)
        if m and m.group("file").endswith(".cs") and _INDENT_MSG.search(m.group("msg")):
            path = m.group("file")
            if path not in cache:
                try:
                    with open(path, encoding="utf-8-sig", errors="replace") as src:
                        cache[path] = string_lines(src.read().replace("\r\n", "\n"))
                except OSError:
                    cache[path] = set()
            if int(m.group("line")) in cache[path]:
                dropped += 1
                continue
        kept.append(entry)

    with open(report_path, "w", encoding="utf-8") as f:
        f.write("".join(e + "\n" for e in kept))

    print(f"Ignored {dropped} indentation error(s) inside C# string literals")
    return 0


if __name__ == "__main__":
    sys.exit(main())
