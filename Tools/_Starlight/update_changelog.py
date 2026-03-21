import os
import yaml
import re
from datetime import datetime
from github import Github

print("Environment Variables:")
changelog_path = os.getenv("CHANGELOG_FILE_PATH")
pr_number = os.getenv("PR_NUMBER")
repo_name = os.getenv("GITHUB_REPOSITORY")
github_token = os.getenv("GITHUB_TOKEN")
print(f"CHANGELOG_FILE_PATH: {changelog_path}")
print(f"PR_NUMBER: {pr_number}")
print(f"GITHUB_REPOSITORY: {repo_name}")
print(f"GITHUB_TOKEN is set: {bool(github_token)}")

g = Github(github_token)
repo = g.get_repo(repo_name)
pr = repo.get_pull(int(pr_number))

def remove_comments(pr_body):
    """
    Removes all content inside HTML comments <!-- --> from the PR body.
    """
    pattern = r"<!--.*?-->"
    cleaned_body = re.sub(pattern, "", pr_body, flags=re.DOTALL)
    return cleaned_body

def parse_changelog(pr_body, pr_author):
    blocks = []
    lines = pr_body.splitlines()

    i = 0
    while i < len(lines):
        line = lines[i].strip()

        if line.startswith(":cl:"):
            author_raw = line.replace(":cl:", "").strip()
            author = author_raw if author_raw else pr_author
            i += 1

            changes = []
            while i < len(lines) and not lines[i].strip().startswith(":cl:"):
                change_match = re.match(r"-\s+(add|remove|tweak|fix):\s+(.+)", lines[i].strip())
                if change_match:
                    changes.append({
                        "type": change_match.group(1).capitalize(),
                        "message": change_match.group(2).strip()
                    })
                i += 1

            if changes:
                blocks.append({
                    "author": author,
                    "changes": changes
                })
        else:
            i += 1

    return blocks

def get_last_id(changelog_data):
    if not changelog_data or "Entries" not in changelog_data or not changelog_data["Entries"]:
        return 0
    return max(entry["id"] for entry in changelog_data["Entries"])

def update_changelog():
    if not pr.body:
        print("PR body is empty.")
        return

    print("Original PR Body:", repr(pr.body))
    cleaned_body = remove_comments(pr.body)
    print("Cleaned PR Body:", repr(cleaned_body))

    if ":cl:" in cleaned_body:
        print("Found ':cl:' in PR body after removing comments.")
        merge_time = pr.merged_at
        blocks = parse_changelog(cleaned_body, pr.user.login)

        print("Parsed entries:", blocks)

        if not blocks:
            print("No changelog entries found after parsing.")
            return

        if os.path.exists(changelog_path):
            print(f"Changelog file exists at {changelog_path}")
            with open(changelog_path, "r", encoding='utf-8') as file:
                changelog_data = yaml.safe_load(file) or {"Entries": []}
        else:
            print(f"Changelog file does not exist and will be created at {changelog_path}")
            changelog_data = {"Entries": []}
        calculatedID = (int(pr_number) * 100)
        for i, block in enumerate(blocks, start=1):
            #shift PR number up two digits
            #add current ID to it
            # e.g., PR number 123 -> calculatedID = (123 * 100) = 12300
            changelog_entry = {
                "author": block["author"],
                "changes": block["changes"],
                "id": calculatedID + i,
                "time": merge_time.isoformat(timespec='microseconds'),
                "url": f"https://github.com/{repo_name}/pull/{pr_number}"
            }
            changelog_data["Entries"].append(changelog_entry)

        os.makedirs(os.path.dirname(changelog_path), exist_ok=True)

        with open(changelog_path, "w", encoding='utf-8') as file:
            yaml.dump(changelog_data, file, allow_unicode=True)
            file.write('\n')
        print(f"Changelog updated and written to {changelog_path}")
    else:
        print("No ':cl:' tag found in PR body after removing comments.")
        return

if __name__ == "__main__":
    update_changelog()
