#!/usr/bin/env python3

"""
Partitions test classes across shards for parallel CI execution.

Reads `dotnet test --list-tests` output from stdin,
outputs a --filter expression for the tests assigned to the given shard.

Usage:
    dotnet test --list-tests ... | python3 test_shard_filter.py <shard-index> <total-shards>

Exit codes:
    0 - success (filter printed, or no tests for this shard)
    1 - error (bad arguments or no tests discovered)
"""

import sys
import hashlib


def main():
    if len(sys.argv) != 3:
        print(f"Usage: {sys.argv[0]} <shard-index> <total-shards>", file=sys.stderr)
        sys.exit(1)

    shard = int(sys.argv[1])
    total = int(sys.argv[2])

    lines = sys.stdin.read().splitlines()

    # Parse test names from `dotnet test --list-tests` output.
    # They appear after the "The following Tests are available:" header, indented.
    tests = []
    in_list = False
    for line in lines:
        stripped = line.strip()
        if "The following Tests are available:" in stripped:
            in_list = True
            continue
        if in_list and stripped:
            tests.append(stripped)

    if not tests:
        print("Error: no tests discovered from input", file=sys.stderr)
        sys.exit(1)

    # Extract unique test fixture (class) names.
    # NUnit FQN format: Namespace.Class.Method or Namespace.Class.Method(params)
    classes = set()
    for test in tests:
        name = test.split("(")[0].strip()  # strip parameters
        dot = name.rfind(".")
        if dot > 0:
            classes.add(name[:dot])
        else:
            classes.add(name)

    # Assign classes to this shard by stable hash
    my_classes = sorted(
        cls for cls in classes
        if int(hashlib.sha256(cls.encode()).hexdigest(), 16) % total == shard
    )

    if not my_classes:
        # No tests for this shard — normal, not an error
        return

    # Build a --filter expression using FullyQualifiedName contains (~)
    print(" | ".join(f"FullyQualifiedName~{cls}" for cls in my_classes))


if __name__ == "__main__":
    main()
