import yaml
import re
import sys
import string

def sort_tags(yaml_file):
    with open(yaml_file, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    header_lines = []
    start_index = None
    for i, line in enumerate(lines):
        if line.strip().startswith("- type: Tag"):
            start_index = i
            break
        if (line.strip() != '#######' and '#  A  #' not in line.strip()):
            header_lines.append(line)

    if start_index is None:
        print("❌ Can't found any Tag!")
        return

    body_lines = lines[start_index:]

    tag_blocks = []
    current_block = []
    inside_block = False

    for line in body_lines:
        if re.match(r'\s*-\s*type:\s*Tag', line):
            if current_block:
                tag_blocks.append(current_block)
            current_block = [line]
            inside_block = True
        elif inside_block and (line.strip() == '' or line.startswith('#')):
            inside_block = False
            tag_blocks.append(current_block)
            current_block = []
        elif inside_block:
            current_block.append(line)

    if current_block:
        tag_blocks.append(current_block)

    def extract_id(block):
        for line in block:
            match = re.match(r'\s*id:\s*(\S+)', line)
            if match:
                return match.group(1)
        return ''

    tag_blocks.sort(key=extract_id)
    groups = {letter: [] for letter in string.ascii_uppercase}

    for block in tag_blocks:
        tag_id = extract_id(block)
        if not tag_id:
            continue
        first_char = tag_id[0].upper()
        if first_char not in groups:
            first_char = '#'
        groups[first_char].append(block)

    sorted_lines = []

    sorted_lines.extend(header_lines)
    if not header_lines[-1].endswith("\n"):
        sorted_lines.append("\n")

    sorted_lines.append("\n")
    sorted_lines.append("\n")

    for letter in string.ascii_uppercase:
        sorted_lines.append("#######\n")
        sorted_lines.append(f"#  {letter}  #\n")
        sorted_lines.append("#######\n\n\n")

        blocks = groups[letter]
        for block in blocks:
            sorted_lines.extend(block)
            if not block[-1].endswith("\n"):
                sorted_lines.append("\n")
            sorted_lines.append("\n")

        sorted_lines.append("\n")

    with open(yaml_file, 'w', encoding='utf-8') as f:
        f.writelines(sorted_lines)

def main():
    if len(sys.argv) != 2:
        print("Drag and drop a YAML file onto this script to alphabetize and group tags by letter.")
        input("Press any key to continue...")
        sys.exit(1)
    file_path = sys.argv[1]

    sort_tags(file_path)

    print(f"Tags in {file_path} have been alphabetized and grouped by starting letter.")
    input("Press any key to continue...")

if __name__ == "__main__":
    main()
