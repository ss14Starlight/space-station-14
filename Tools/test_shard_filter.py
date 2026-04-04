#!/usr/bin/env python3

"""
Partitions test classes across shards for parallel CI execution.

Mode 1 - Generate all shard filters to files:
    dotnet test --list-tests ... | python3 test_shard_filter.py generate <total-shards> <output-dir>
    Writes <output-dir>/shard_0.filter .. shard_N.filter

Mode 2 - Read a pre-generated filter file:
    python3 test_shard_filter.py read <filter-file>
    Prints the filter to stdout (empty output if file is empty/missing)

Exit codes:
    0 - success
    1 - error (bad arguments or no tests discovered in generate mode)
"""

import sys
import os


def parse_tests(lines):
    """Parse test names from `dotnet test --list-tests` output."""
    tests = []
    in_list = False
    for line in lines:
        stripped = line.strip()
        if "The following Tests are available:" in stripped:
            in_list = True
            continue
        if in_list and stripped:
            tests.append(stripped)
    return tests


def extract_classes(tests):
    """Extract unique test fixture (class) names with test counts from FQN test names."""
    counts = {}
    for test in tests:
        name = test.split("(")[0].strip()
        dot = name.rfind(".")
        cls = name[:dot] if dot > 0 else name
        counts[cls] = counts.get(cls, 0) + 1
    return counts


def build_filter(classes):
    """Build a --filter expression from class names."""
    if not classes:
        return ""
    return " | ".join(f"FullyQualifiedName~{cls}" for cls in sorted(classes))


def cmd_generate():
    if len(sys.argv) != 4:
        print(f"Usage: {sys.argv[0]} generate <total-shards> <output-dir>", file=sys.stderr)
        sys.exit(1)

    total = int(sys.argv[2])
    output_dir = sys.argv[3]

    lines = sys.stdin.read().splitlines()
    tests = parse_tests(lines)

    if not tests:
        print("Error: no tests discovered from input", file=sys.stderr)
        sys.exit(1)

    class_counts = extract_classes(tests)
    print(f"Discovered {len(tests)} tests in {len(class_counts)} classes, distributing across {total} shards", file=sys.stderr)

    os.makedirs(output_dir, exist_ok=True)

    # Greedy load-balancing: assign heaviest classes first to least-loaded shard
    shards = [[] for _ in range(total)]
    shard_loads = [0] * total
    for cls in sorted(class_counts, key=lambda c: class_counts[c], reverse=True):
        lightest = min(range(total), key=lambda s: shard_loads[s])
        shards[lightest].append(cls)
        shard_loads[lightest] += class_counts[cls]

    for shard in range(total):
        my_classes = sorted(shards[shard])
        filter_expr = build_filter(my_classes)
        path = os.path.join(output_dir, f"shard_{shard}.filter")
        with open(path, "w") as f:
            f.write(filter_expr)
        print(f"  Shard {shard}: {len(my_classes)} classes, {shard_loads[shard]} tests", file=sys.stderr)
        for cls in my_classes:
            print(f"    - {cls} ({class_counts[cls]} tests)", file=sys.stderr)


def cmd_read():
    if len(sys.argv) != 3:
        print(f"Usage: {sys.argv[0]} read <filter-file>", file=sys.stderr)
        sys.exit(1)

    path = sys.argv[2]
    if not os.path.exists(path):
        return
    with open(path) as f:
        content = f.read().strip()
    if content:
        # Print human-readable class list to stderr
        classes = [part.replace("FullyQualifiedName~", "").strip() for part in content.split("|")]
        print(f"Running {len(classes)} test classes:", file=sys.stderr)
        for cls in classes:
            print(f"  - {cls}", file=sys.stderr)
        print(content)


def main():
    if len(sys.argv) < 2:
        print(f"Usage: {sys.argv[0]} <generate|read> ...", file=sys.stderr)
        sys.exit(1)

    cmd = sys.argv[1]
    if cmd == "generate":
        cmd_generate()
    elif cmd == "read":
        cmd_read()
    else:
        print(f"Unknown command: {cmd}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
