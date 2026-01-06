import os
import sys
import re
from github import Github

GITHUB_TOKEN = os.getenv('GITHUB_TOKEN')
PR_NUMBER = os.getenv('PR_NUMBER')
GITHUB_REPOSITORY = os.getenv('GITHUB_REPOSITORY')

if not all([GITHUB_TOKEN, PR_NUMBER, GITHUB_REPOSITORY]):
    print("::error::Missing required environment variables")
    sys.exit(1)

g = Github(GITHUB_TOKEN)
repo = g.get_repo(GITHUB_REPOSITORY)
pr = repo.get_pull(int(PR_NUMBER))

pr_body = pr.body or ""

# Check if changelog section exists
if "**Changelog**" not in pr_body:
    print("No changelog section found, skipping validation.")
    sys.exit(0)

# Extract changelog content
changelog_match = re.search(r'\*\*Changelog\*\*\s*(.*?)$', pr_body, re.DOTALL)
if not changelog_match or not changelog_match.group(1).strip():
    print("::error::Changelog section is empty. Please add changelog entries or remove the section.")
    sys.exit(1)

changelog_content = changelog_match.group(1).strip()

# Remove comments from changelog body
changelog_without_comments = re.sub(r'<!--.*?-->', '', changelog_content, flags=re.DOTALL).strip()

# Check if there any content left after removing comments
if not changelog_without_comments:
    print("::error::Changelog section contains only comments. Please add changelog entries or remove the section.")
    sys.exit(1)

# Check for :cl: command
if ":cl:" not in changelog_without_comments:
    print("::error::Changelog is missing the :cl: command")
    sys.exit(1)

# Check that after :cl: there is a non-empty author
cl_line = None
for line in changelog_without_comments.splitlines():
    if line.strip().startswith(':cl:'):
        cl_line = line
        break

if cl_line is None or not cl_line.strip()[4:].strip():
    print("::error::After ':cl:' you must specify your nickname e.g. ':cl: Rinary'")
    sys.exit(1)

# Check for valid tags 
valid_tags = ["add", "remove", "tweak", "fix"]
entry_pattern = re.compile(r'^[ \t]*[^a-zA-Z0-9]?[ \t]*(add|remove|tweak|fix):', re.MULTILINE)
entries = entry_pattern.findall(changelog_without_comments)

if not entries:
    print("::error::No changelog entries found. You must add at least one entry with a valid tag (add, remove, tweak, fix)")
    sys.exit(1)

invalid_entries = [tag for tag in entries if tag not in valid_tags]
if invalid_entries:
    print(f"::error::Invalid changelog tags found: {', '.join(invalid_entries)}. Valid tags are: {', '.join(valid_tags)}")
    sys.exit(1)

# Mapping tag -> required description prefix
required_prefixes = {
    "add": "Added ",
    "remove": "Removed ",
    "tweak": "Changed ",
    "fix": "Fixed ",
}

# Amount of symbols which will be printed if we find an error in description
# Like: "Line 10: 'add: First' entries must start with 'Added '"
# Where 'First' is 5 symbols
amount_of_first_description_symbols = 5

invalid_lines = []

matched_lines_count = 0

for line_number, line in enumerate(changelog_without_comments.splitlines(), start=1):
    line = line.strip()

    # Match lines like "- add: Something"
    match = re.match(r'^[-*]?\s*(add|remove|tweak|fix):\s*(.+)', line)
    if not match:
        continue

    matched_lines_count += 1
    tag, description = match.groups()
    required_prefix = required_prefixes[tag]

    if not description.startswith(required_prefix):
        invalid_lines.append(
            f"Line {line_number}: '{tag}: {description[:amount_of_first_description_symbols]}' entries must start with '{required_prefix.strip()}'"
        )
    
    if not description.endswith('.'):
        invalid_lines.append(
            f"Line {line_number}: '{tag}: {description[:amount_of_first_description_symbols]}' entries must end with a period."
        )

if matched_lines_count == 0:
    invalid_lines.append("Changelog entries must follow the format: 'tag: description'")

if invalid_lines:
    for error in invalid_lines:
        print(f"::error::{error}")
    sys.exit(1)