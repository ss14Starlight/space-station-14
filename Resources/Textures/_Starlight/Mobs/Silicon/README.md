# Borg headwear displacement

Each map in `borg_headwear_displacements.rsi` positions headwear on a borg model using four 32×32 frames: south/north on the top row, east/west below. Models with the same head placement can share a map.

Modify maps with Aseprite scripts in `Tools/SS14 Aseprite Plugins`

- For these maps, the shift tool moves headwear half a pixel, which is required to fix situations where hats with odd numbers of pixels could not center on borg heads (even number of pixels).
- Aseprite may show half pixel modifications incorrectly.

For a new map, copy an existing state, register it in `meta.json`, and reference it in `Resources/Prototypes/_Starlight/Inventory/InventoryTemplates/borg_headwear.yml`.
Assign the template in the borg subtype partial. Slot offsets also affect placement: 1/32 unit equals one pixel.
