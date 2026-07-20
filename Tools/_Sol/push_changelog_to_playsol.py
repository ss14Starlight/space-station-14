#!/usr/bin/env python3
"""Push ChangelogSol.yml to the PlaySol site (UFS changelogs)."""

from __future__ import annotations

import json
import os
import sys
import urllib.error
import urllib.request

CHANGELOG_PATH = os.environ.get(
    "CHANGELOG_FILE_PATH", "Resources/Changelog/ChangelogSol.yml"
)
API_BASE = os.environ.get("PLAYSOL_API_BASE", "").rstrip("/")
DEPLOY_SECRET = os.environ.get("PLAYSOL_DEPLOY_SECRET", "")
CHANNEL = os.environ.get("PLAYSOL_CHANGELOG_CHANNEL", "main")
# Paths from secrets so they are not hardcoded in workflow YAML
AUTH_PATH = os.environ.get("PLAYSOL_AUTH_PATH", "").strip()
CHANGELOG_API_PATH = (
    os.environ.get("PLAYSOL_CHANGELOG_PATH") or "/api/v1/changelogs/{channel}"
).replace("{channel}", CHANNEL)

if not AUTH_PATH:
    print("PLAYSOL_AUTH_PATH is required", file=sys.stderr)
    raise SystemExit(1)
if not AUTH_PATH.startswith("/"):
    AUTH_PATH = "/" + AUTH_PATH
if not CHANGELOG_API_PATH.startswith("/"):
    CHANGELOG_API_PATH = "/" + CHANGELOG_API_PATH


def http_json(method: str, url: str, data: bytes | None = None, headers: dict | None = None):
    req = urllib.request.Request(url, data=data, method=method, headers=headers or {})
    with urllib.request.urlopen(req, timeout=60) as resp:
        body = resp.read()
        if not body:
            return {}
        return json.loads(body.decode())


def main() -> int:
    if not API_BASE or not DEPLOY_SECRET:
        print("PLAYSOL_API_BASE and PLAYSOL_DEPLOY_SECRET are required", file=sys.stderr)
        return 1
    if CHANNEL not in ("main", "testing"):
        print(f"Invalid channel: {CHANNEL}", file=sys.stderr)
        return 1
    if not os.path.isfile(CHANGELOG_PATH):
        print(f"Missing changelog file: {CHANGELOG_PATH}", file=sys.stderr)
        return 1

    with open(CHANGELOG_PATH, "rb") as f:
        yaml_bytes = f.read()

    try:
        token_resp = http_json(
            "POST",
            f"{API_BASE}{AUTH_PATH}",
            data=json.dumps({"scope": ["changelog"]}).encode(),
            headers={
                "Authorization": f"Bearer {DEPLOY_SECRET}",
                "Content-Type": "application/json",
            },
        )
        token = token_resp["token"]
        result = http_json(
            "POST",
            f"{API_BASE}{CHANGELOG_API_PATH}",
            data=yaml_bytes,
            headers={
                "Authorization": f"Bearer {token}",
                "Content-Type": "text/yaml",
            },
        )
    except urllib.error.HTTPError as e:
        print(e.read().decode(errors="replace"), file=sys.stderr)
        raise

    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
