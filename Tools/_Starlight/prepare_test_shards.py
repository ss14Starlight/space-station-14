#!/usr/bin/env python3

import os
import sys
import subprocess
from pathlib import Path


SHARD_COUNT = 6

def main():
    script_dir = Path(__file__).resolve().parent
    project_root = script_dir.parent.parent # Project root is two folders up
    os.chdir(project_root)

    filter_dir = ".integration-filters"

    env = os.environ.copy()

    # Prepend ~/.dotnet to PATH so the dotnet CLI is found on per-user installations
    dotnet_home = Path.home() / ".dotnet"
    env["PATH"] = str(dotnet_home) + os.pathsep + env.get("PATH", "")

    # Build the tests, basically a no-op if already built
    print("Building Content.IntegrationTests...", file=sys.stderr)
    subprocess.run(
        [
            "dotnet", "build",
            "--configuration", "DebugOpt",
            "/m",
            "Content.IntegrationTests/Content.IntegrationTests.csproj",
        ],
        env=env,
        check=True,
    )

    # Grab the names of all tests
    print("Generating shard filters...", file=sys.stderr)
    test_app = project_root / "bin" / "Content.IntegrationTests" / "Content.IntegrationTests"
    if os.name == "nt":
        test_app = test_app.with_name(test_app.name + ".exe")
    list_result = subprocess.run(
        [str(test_app), "--list-tests", "json"],
        env=env,
        stdout=subprocess.PIPE,
        text=True,
        check=True,
    )

    # Pipe the test names into the partitioning script to generate the shard filters.
    # Why not just load the other script or something? Well, it's already invoked from CI in this manner,
    # so it's easier to shape this script to just pipe to it instead of editing that script to act in multiple ways
    # depending on how it's invoked.
    filter_script = script_dir / "partition_tests.py"
    subprocess.run(
        [sys.executable, filter_script, "generate", str(SHARD_COUNT), filter_dir],
        input=list_result.stdout,
        text=True,
        check=True,
    )

if __name__ == "__main__":
    main()
