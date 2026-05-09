import os
import sys
from PIL import Image

# ===== Options =====
CHUNK_SIZE = 256
OUTPUT_DIR = "chunks"
# =====================


def get_next_grid_id():
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    grid_id = 0
    while os.path.exists(os.path.join(OUTPUT_DIR, str(grid_id))):
        grid_id += 1

    return grid_id


def slice_map(image_path):
    img = Image.open(image_path)
    img_dir = os.path.dirname(os.path.abspath(image_path))
    width, height = img.size

    grid_id = get_next_grid_id()
    grid_path = os.path.join(img_dir, OUTPUT_DIR, str(grid_id))

    print(f"Creating grid {grid_id}")

    for y in range(0, height, CHUNK_SIZE):
        row = y // CHUNK_SIZE

        for x in range(0, width, CHUNK_SIZE):
            col = x // CHUNK_SIZE

            chunk = img.crop((x, y, x + CHUNK_SIZE, y + CHUNK_SIZE))

            chunk_dir = os.path.join(grid_path, str(row), str(col))
            os.makedirs(chunk_dir, exist_ok=True)

            chunk_path = os.path.join(chunk_dir, "chunk.png")
            chunk.save(chunk_path)

            print(f"chunk: row {row} col {col}")

    print("Done!")


def main():
    if len(sys.argv) < 2:
        print("Drag PNG file on script or enter path:")
        print("python map_slicer.py map.png")
        return

    image_path = sys.argv[1]

    if not os.path.exists(image_path):
        print("File can't be founded")
        return

    slice_map(image_path)


if __name__ == "__main__":
    main()
