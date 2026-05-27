import os
import sys
import subprocess
from datetime import datetime
from PIL import Image

# ========= Options =========

CHUNK_SIZE = 256

# output renderer dir
RENDER_OUTPUT_DIR = "./Resources/MapImages"

# chunk output
CHUNK_OUTPUT_DIR = "map"

# log file
LOG_FILE = "render_log.txt"

# output viewer json?
VIEWER_JSON = True

# output parallax?
PARALLAX = False # Note: Parallax output doesn't work how it's supposed to be, It's isn't per-map parallax, it just saves default parallax at MapImages. So it's useless thing.

# ==============================


def log(message):
    time = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    text = f"[{time}] {message}"
    print(text)

    with open(LOG_FILE, "a", encoding="utf8") as f:
        f.write(text + "\n")


def get_next_grid_id(path):
    os.makedirs(path, 511, exist_ok=True)

    grid = 0
    while os.path.exists(os.path.join(path, CHUNK_OUTPUT_DIR, str(grid))):
        grid += 1

    return grid


def slice_map(image_path):
    try:
        img = Image.open(image_path)
    except Exception as e:
        log(f"Image opening error {image_path}: {e}")
        return

    width, height = img.size

    img_dir = os.path.dirname(os.path.abspath(image_path))
    grid_id = get_next_grid_id(img_dir)

    log(f"Map slicing -> grid {grid_id}")

    for x in range(0, width, CHUNK_SIZE):
        col = x // CHUNK_SIZE

        for y in range(0, height, CHUNK_SIZE):
            row = y // CHUNK_SIZE

            chunk = img.crop((x, y, x + CHUNK_SIZE, y + CHUNK_SIZE))

            chunk_dir = os.path.join(img_dir, CHUNK_OUTPUT_DIR, str(grid_id), str(col), str(row))
            os.makedirs(chunk_dir, exist_ok=True)

            chunk.save(os.path.join(chunk_dir, "0"), format="PNG")

    log("Cutting completed")


def render_map(map_id):

    log(f"Render map {map_id}")

    cmd = [
        "dotnet",
        "run",
        "--project",
        "Content.MapRenderer",
        map_id
    ]

    if (VIEWER_JSON):
        cmd.append("--viewer")

    if (PARALLAX):
        cmd.append("--parallax")

    try:
        subprocess.run(cmd, check=True)
    except subprocess.CalledProcessError:
        log(f"Map rendering error {map_id}")
        return False

    return True


def find_rendered_maps(map_id):

    folder = os.path.abspath(os.path.join(RENDER_OUTPUT_DIR, map_id))

    if not os.path.exists(folder):
        log(f"Cant find render for {map_id} in {folder}")
        return []

    files = []

    for name in os.listdir(folder):

        lower = name.lower()

        if lower.startswith(map_id.lower() + "-") and lower.endswith(".png"):
            files.append(os.path.join(folder, name))

    return sorted(files)


def process_map(map_id):

    success = render_map(map_id)

    if not success:
        return

    images = find_rendered_maps(map_id)

    if not images:
        log(f"Map render {map_id} not found")
        return

    for image_path in images:
        log(f"Render found: {image_path}")
        slice_map(image_path)


def load_maps_from_txt(path):

    with open(path, "r", encoding="utf8") as f:
        return [line.strip() for line in f if line.strip()]


def main():

    if len(sys.argv) > 1:

        txt = sys.argv[1]

        if os.path.exists(txt):
            maps = load_maps_from_txt(txt)
        else:
            maps = sys.argv[1:]

    else:
        maps = input("Enter map_id through a space: ").split()

    if not maps:
        print("No maps to process")
        return

    log(f"Start of maps processing: {maps}")

    already_processed = []
    confirmation = ""

    for map_id in maps:
        possible_renderers = find_rendered_maps(map_id)
        if not possible_renderers:
            continue
        if confirmation == "":
            confirmation = input("Founded already rendered image, slice it? Write 'true' to confirm.")
        if confirmation.lower() == "true":
            already_processed.append(map_id)
            for image_path in possible_renderers:
                log(f"Render found: {image_path}")
                slice_map(image_path)

    for map_id in maps:
        if map_id in already_processed:
            continue
        process_map(map_id)

    log("All maps have been processed.")


if __name__ == "__main__":
    main()
