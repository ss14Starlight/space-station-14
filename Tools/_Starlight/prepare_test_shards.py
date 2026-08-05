#!/usr/bin/env python3

import os
import sys
import subprocess
from pathlib import Path


def main():
    script_dir = Path(__file__).resolve().parent
    project_root = script_dir.parent.parent # Project root is two folders up
    os.chdir(project_root)

    filter_dir = ".integration-filters"

    if (project_root / filter_dir / "shard_0.runsettings").is_file():
        print("Shard filters already generated, skipping.", file=sys.stderr)
        return

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
            "--no-restore",
            "/m",
            "Content.IntegrationTests/Content.IntegrationTests.csproj",
        ],
        env=env,
        check=True,
    )

    # Grab the names of all tests
    print("Generating shard filters...", file=sys.stderr)
    list_result = subprocess.run(
        [
            "dotnet", "test",
            "--list-tests",
            "--no-build",
            "--configuration", "DebugOpt",
            "Content.IntegrationTests/Content.IntegrationTests.csproj",
        ],
        env=env,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT, # merge stderr into stdout
        text=True,
        check=True,
    )

    # Pipe the test names into the partitioning script to generate the shard filters.
    # Why not just load the other script or something? Well, it's already invoked from CI in this manner,
    # so it's easier to shape this script to just pipe to it instead of editing that script to act in multiple ways
    # depending on how it's invoked.
    filter_script = script_dir / "partition_tests.py"
    subprocess.run(
        [sys.executable, filter_script, "generate", "8", filter_dir],
        input=list_result.stdout,
        text=True,
        check=True,
    )

if __name__ == "__main__":
    main()
