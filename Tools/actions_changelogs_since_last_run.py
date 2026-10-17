#!/usr/bin/env python3

"""
Sends updates to a Discord webhook for new changelog entries since the last GitHub Actions publish run.

Automatically figures out the last run and changelog contents with the GitHub API.
"""

import itertools
import os
from pathlib import Path
from typing import Any, Iterable

import requests
import sys
import yaml
import time

DEBUG = False
DEBUG_CHANGELOG_FILE_OLD = Path("Resources/Changelog/Old.yml")
GITHUB_API_URL = os.environ.get("GITHUB_API_URL", "https://api.github.com")

DISCORD_WEBHOOK_URL = os.environ.get("DISCORD_WEBHOOK_URL")
DISCORD_CHANGELOG_ROLE_ID = int(os.environ.get("DISCORD_CHANGELOG_ROLE_ID", "1308143973684088883"))

CHANGELOG_FILE = "Resources/Changelog/ChangelogStarlight.yml"
# Must match the changelog job name in .github/workflows/publish.yml and publish-testing.yml
CHANGELOG_JOB_NAME = "Publish Changelogs"
MAX_RUN_PAGES = 10
TYPES_TO_EMOJI = {"Fix": "🐛", "Add": "🆕", "Remove": "❌", "Tweak": "⚒️"}
ChangelogEntry = dict[str, Any]

EMBED_DESCRIPTION_LIMIT = 4096
EMBED_TITLE_LIMIT = 256
EMBED_FIELD_NAME_LIMIT = 256
EMBED_FIELD_VALUE_LIMIT = 1024


def main():
    if not DISCORD_WEBHOOK_URL:
        # Fail the job: a successful changelog job is what later runs treat as "already sent".
        print("No webhook URL; cannot send changelogs", file=sys.stderr)
        sys.exit(1)

    if DEBUG:
        last_changelog_stream = DEBUG_CHANGELOG_FILE_OLD.read_text()
    else:
        last_changelog_stream = get_last_changelog()

    last_changelog = yaml.safe_load(last_changelog_stream) or {}
    with open(CHANGELOG_FILE, "r") as f:
        cur_changelog = yaml.safe_load(f) or {}

    new_entries = list(diff_changelog(last_changelog, cur_changelog))
    print(f"{len(new_entries)} new changelog entries to send.")
    if not new_entries:
        print("No new entries to report.")
        return

    ping_role_once(str(DISCORD_CHANGELOG_ROLE_ID))

    pr_groups = group_entries_by_pr(new_entries)
    for pr_id, entries in pr_groups.items():
        embed = build_embed_for_pr(pr_id, entries)
        send_embed(embed)


def get_most_recent_workflow(
    sess: requests.Session, github_repository: str, github_run: str
) -> Any:
    """
    Finds the latest previous run of this workflow whose changelog job actually finished successfully.

    The changelog job runs independently of the build job, so the overall run conclusion is irrelevant:
    a run whose build failed or was cancelled (concurrency cancel-in-progress) has still sent its changelogs.
    """
    current = get_current_run(sess, github_repository, github_run)

    for page in range(1, MAX_RUN_PAGES + 1):
        # No query filters (branch, status, created, ...): GitHub serves those from its search index,
        # which can return a stale or partial list and make us pick a months-old run as the base.
        # The unfiltered list is newest first, so filter on our side.
        resp = sess.get(f"{current['workflow_url']}/runs", params={"per_page": 100, "page": page})
        resp.raise_for_status()
        runs = resp.json().get("workflow_runs", [])
        if not runs:
            break

        for run in runs:
            if run["id"] == current["id"] or run["head_branch"] != current["head_branch"]:
                continue
            if run["created_at"] > current["created_at"]:
                continue
            if changelog_job_succeeded(sess, run):
                return run

    raise RuntimeError("No previous run with a successful changelog job found")


def changelog_job_succeeded(sess: requests.Session, run: Any) -> bool:
    resp = sess.get(run["jobs_url"], params={"per_page": 100})
    resp.raise_for_status()
    for job in resp.json().get("jobs", []):
        if job["name"] == CHANGELOG_JOB_NAME:
            return job["status"] == "completed" and job["conclusion"] == "success"
    return False


def get_current_run(
    sess: requests.Session, github_repository: str, github_run: str
) -> Any:
    resp = sess.get(f"{GITHUB_API_URL}/repos/{github_repository}/actions/runs/{github_run}")
    resp.raise_for_status()
    return resp.json()


def get_last_changelog() -> str:
    github_repository = os.environ["GITHUB_REPOSITORY"]
    github_run = os.environ["GITHUB_RUN_ID"]
    github_token = os.environ["GITHUB_TOKEN"]

    session = requests.Session()
    session.headers["Authorization"] = f"Bearer {github_token}"
    session.headers["Accept"] = "Accept: application/vnd.github+json"
    session.headers["X-GitHub-Api-Version"] = "2022-11-28"

    most_recent = get_most_recent_workflow(session, github_repository, github_run)
    last_sha = most_recent["head_sha"]
    print(f"Last run with sent changelogs was {most_recent['id']} ({most_recent['created_at']}): {last_sha}")
    return get_last_changelog_by_sha(session, last_sha, github_repository)


def get_last_changelog_by_sha(
    sess: requests.Session, sha: str, github_repository: str
) -> str:
    params = {"ref": sha}
    headers = {"Accept": "application/vnd.github.raw"}
    resp = sess.get(
        f"{GITHUB_API_URL}/repos/{github_repository}/contents/{CHANGELOG_FILE}",
        headers=headers,
        params=params,
    )
    resp.raise_for_status()
    return resp.text


def diff_changelog(old: dict[str, Any], cur: dict[str, Any]) -> Iterable[ChangelogEntry]:
    # Compare by membership: ids are PR-number based, so a later-merged older PR gets a lower id.
    old_ids = {e["id"] for e in old.get("Entries", [])}
    if not old_ids:
        # Never dump the whole changelog because the previous one could not be read.
        raise RuntimeError("Previous changelog has no entries; refusing to resend everything")

    return (e for e in cur.get("Entries", []) if e["id"] not in old_ids)


def group_entries_by_pr(entries: Iterable[ChangelogEntry]) -> dict[str, list[ChangelogEntry]]:
    groups: dict[str, list[ChangelogEntry]] = {}
    for entry in entries:
        url = entry.get("url", "")
        if url and url.strip():
            pr_number = url.rstrip("/").split("/")[-1]
        else:
            pr_number = "no-pr"
        groups.setdefault(pr_number, []).append(entry)
    return groups


def build_embed_for_pr(pr_id: str, entries: list[ChangelogEntry]) -> dict[str, Any]:
    authors = set()
    description_lines: list[str] = []

    for entry in entries:
        authors.add(entry.get("author", "Unknown"))
        url = entry.get("url", "").strip() or None
        for change in entry.get("changes", []):
            emoji = TYPES_TO_EMOJI.get(change.get("type", ""), "❓")
            message = change.get("message", "").strip()
            if len(message) > 300:
                message = message[:297].rstrip() + "..."
            line = f"{emoji} {message}"
            if url and pr_id != "no-pr":
                line += f" ([#{pr_id}]({url}))"
            description_lines.append(line)

    description = "\n".join(description_lines)
    if len(description) > EMBED_DESCRIPTION_LIMIT:
        description = description[: EMBED_DESCRIPTION_LIMIT - 50].rstrip() + "\n*...truncated...*"

    sorted_authors = sorted(authors)
    authors_str = ", ".join(sorted_authors)
    title = authors_str
    if len(title) > EMBED_TITLE_LIMIT:
        # truncate authors part to fit
        overflow = len(title) - EMBED_TITLE_LIMIT + 3  # for "..."
        # remove overflow chars from authors_str
        truncated_authors = authors_str
        if overflow < len(authors_str):
            truncated_authors = authors_str[: -overflow].rstrip()
            # avoid cutting mid-comma: optionally rstrip to last comma-space
            if "," in truncated_authors:
                truncated_authors = truncated_authors.rsplit(",", 1)[0]
            truncated_authors = truncated_authors.rstrip() + "..."
        title = truncated_authors
        if len(title) > EMBED_TITLE_LIMIT:
            title = title[:EMBED_TITLE_LIMIT]

    author_field = ", ".join(sorted_authors)
    embed: dict[str, Any] = {
        "title": title,
        "description": description,
  #      "fields": [
  #          {"name": "Author(s)", "value": author_field[:EMBED_FIELD_VALUE_LIMIT], "inline": False}
  #      ],
        "footer": {"text": "Starlight changelog"},
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }
    if pr_id != "no-pr":
        embed["url"] = entries[0].get("url", "")
    return embed


def send_embed(embed: dict[str, Any]):
    payload = {
        "embeds": [embed],
        "allowed_mentions": {"parse": []},  # no automatic pings
    }
    post_with_retries(payload)


def ping_role_once(role_id: str):
    content = f"<@&{role_id}> New changelog updates are ready for release."
    payload = {
        "content": content,
        "allowed_mentions": {"roles": [int(role_id)]},
    }
    post_with_retries(payload)


def post_with_retries(payload: dict[str, Any]):
    attempt = 0
    while True:
        try:
            resp = requests.post(DISCORD_WEBHOOK_URL, json=payload, timeout=10)
            if resp.status_code == 429:
                attempt += 1
                if attempt > 20:
                    print("Too many rate limit retries; giving up", file=sys.stderr)
                    sys.exit(1)
                retry_after = resp.json().get("retry_after", 5)
                print(f"Rate limited; sleeping {retry_after}s (attempt {attempt})")
                time.sleep(retry_after)
                continue
            resp.raise_for_status()
            return
        except requests.exceptions.RequestException as e:
            attempt += 1
            if attempt > 5:
                # Fail the job so the next run does not treat these entries as sent.
                print(f"Failed after retries: {e}", file=sys.stderr)
                sys.exit(1)
            backoff = 2 ** attempt
            print(f"Request failed ({e}), backing off {backoff}s and retrying")
            time.sleep(backoff)


if __name__ == "__main__":
    main()
