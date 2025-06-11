import os
import sys
import re
from github import Github

# Get environment variables
GITHUB_TOKEN = os.getenv('GITHUB_TOKEN')
PR_NUMBER = os.getenv('PR_NUMBER')
GITHUB_REPOSITORY = os.getenv('GITHUB_REPOSITORY')

if not all([GITHUB_TOKEN, PR_NUMBER, GITHUB_REPOSITORY]):
    print("::error::Missing required environment variables")
    sys.exit(1)

# Initialize GitHub client
g = Github(GITHUB_TOKEN)
repo = g.get_repo(GITHUB_REPOSITORY)
pr = repo.get_pull(int(PR_NUMBER))

# Get PR description
pr_body = pr.body or ""

# Check if changelog section exists
if "**Changelog**" not in pr_body:
    print("::error::Changelog section is missing. Please add a changelog section to your PR description.")
    sys.exit(1)

# Extract changelog content
changelog_match = re.search(r'\*\*Changelog\*\*\s*(.*?)(?:-->|$)', pr_body, re.DOTALL)
if not changelog_match:
    print("::error::Could not find changelog content")
    sys.exit(1)

changelog_content = changelog_match.group(1).strip()

# Check for :cl: command
if ":cl:" not in changelog_content:
    print("::error::Changelog is missing the :cl: command")
    sys.exit(1)

# Check for valid tags (accepts any symbol or no symbol before tag)
valid_tags = ["add", "remove", "tweak", "fix"]
entry_pattern = re.compile(r'^[ \t]*[^a-zA-Z0-9]?[ \t]*(add|remove|tweak|fix):', re.MULTILINE)
entries = entry_pattern.findall(changelog_content)

invalid_entries = [tag for tag in entries if tag not in valid_tags]
if invalid_entries:
    print(f"::error::Invalid changelog tags found: {', '.join(invalid_entries)}. Valid tags are: {', '.join(valid_tags)}")
    sys.exit(1)

# Check for proper formatting (tag: description)
if not re.search(r'^[ \t]*[^a-zA-Z0-9]?[ \t]*(add|remove|tweak|fix): .+', changelog_content, re.MULTILINE):
    print("::error::Changelog entries must follow the format: 'tag: description'")
    sys.exit(1)

# Check if there are any entries after the command
if not re.search(r':cl:.*\n[ \t]*[^a-zA-Z0-9]?[ \t]*(add|remove|tweak|fix):', changelog_content, re.DOTALL):
    print("::error::No changelog entries found after the command")
    sys.exit(1)

print("Changelog validation passed!") 
# pooo