using Robust.Shared.Serialization;
using DrawDepthTag = Robust.Shared.GameObjects.DrawDepth;

namespace Content.Shared.DrawDepth
{
    [ConstantsFor(typeof(DrawDepthTag))]
    public enum DrawDepth
    {
        /// <summary>
        ///     This is for sub-floors, the floors you see after prying off a tile.
        /// </summary>
        LowFloors = DrawDepthTag.Default - 22, // Carpmosia-edit - 5 pipe layers

        // various entity types that require different
        // draw depths, as to avoid hiding
        #region SubfloorEntities
        // Carpmosia-start - 5 pipe layers
        ThickPipe = DrawDepthTag.Default - 21,
        ThickWire = DrawDepthTag.Default - 20,
        ThinPipeAlt4 = DrawDepthTag.Default - 19,
        ThinPipeAlt3 = DrawDepthTag.Default - 18,
        // Carpmosia-end - 5 pipe layers
        ThinPipeAlt2 = DrawDepthTag.Default - 17,
        ThinPipeAlt1 = DrawDepthTag.Default - 16,
        ThinPipe = DrawDepthTag.Default - 15,
        ThinWire = DrawDepthTag.Default - 14,
        #endregion

        /// <summary>
        ///     Things that are beneath regular floors.
        /// </summary>
        BelowFloor = DrawDepthTag.Default - 13,

        /// <summary>
        ///     Used for entities like carpets.
        /// </summary>
        FloorTiles = DrawDepthTag.Default - 12,

        /// <summary>
        ///     Things that are actually right on the floor, like ice crust or atmos devices. This does not mean objects like
        ///     tables, even though they are technically "on the floor".
        /// </summary>
        FloorObjects = DrawDepthTag.Default - 11,

        /// <summary>
        //     Discrete drawdepth to avoid z-fighting with other FloorObjects but also above floor entities.
        /// </summary>
        Puddles = DrawDepthTag.Default - 10,

        // There's a gap for subfloor entities to retain relative draw depth when revealed by a t-ray scanner.
        /// <summary>
        //     Objects that are on the floor, but should render above puddles. This includes kudzu, holopads, telepads and levers.
        /// </summary>
        HighFloorObjects = DrawDepthTag.Default - 5,

        DeadMobs = DrawDepthTag.Default - 4,

        /// <summary>
        ///     Allows small mobs like mice and drones to render under tables and chairs but above puddles and vents
        /// </summary>
        SmallMobs = DrawDepthTag.Default - 3,

        Walls = DrawDepthTag.Default - 2,

        /// <summary>
        ///     Used for windows (grilles use walls) and misc signage. Useful if you want to have an APC in the middle
        ///     of some wall-art or something.
        /// </summary>
        WallTops = DrawDepthTag.Default - 1,

        /// <summary>
        ///     Furniture, crates, tables. etc. If an entity should be drawn on top of a table, it needs a draw depth
        ///     that is higher than this.
        /// </summary>
        Objects = DrawDepthTag.Default,

        /// <summary>
        ///     In-between an furniture and an item. Useful for entities that need to appear on top of tables, but are
        ///     not items. E.g., power cell chargers. Also useful for pizza boxes, which appear above crates, but not
        ///     above the pizza itself.
        /// </summary>
        SmallObjects = DrawDepthTag.Default + 1,

        /// <summary>
        ///     Posters, APCs, air alarms, etc. This also includes most lights & lamps.
        /// </summary>
        WallMountedItems = DrawDepthTag.Default + 2,

        /// <summary>
        ///     To use for objects that would usually fall under SmallObjects, but appear taller than 1 tile. For example: Reagent Grinder
        /// </summary>
        LargeObjects = DrawDepthTag.Default + 3,

        /// <summary>
        ///     Generic items. Things that should be above crates & tables, but underneath mobs.
        /// </summary>
        Items = DrawDepthTag.Default + 4,
        /// <summary>
        /// Stuff that should be drawn below mobs, but on top of items. Like muzzle flash.
        /// </summary>
        BelowMobs = DrawDepthTag.Default + 5,

        Mobs = DrawDepthTag.Default + 6,

        OverMobs = DrawDepthTag.Default + 7,

        Doors = DrawDepthTag.Default + 8,

        /// <summary>
        /// Blast doors and shutters which go over the usual doors.
        /// </summary>
        BlastDoors = DrawDepthTag.Default + 9,

        /// <summary>
        /// Stuff that needs to draw over most things, but not effects, like Kudzu.
        /// </summary>
        Overdoors = DrawDepthTag.Default + 10,

        /// <summary>
        ///     Explosions, fire, melee swings. Whatever.
        /// </summary>
        Effects = DrawDepthTag.Default + 11,

        Ghosts = DrawDepthTag.Default + 12,

        /// <summary>
        ///    Use this selectively if it absolutely needs to be drawn above (almost) everything else. Examples include
        ///    the pointing arrow, the drag & drop ghost-entity, and some debug tools.
        /// </summary>
        Overlays = DrawDepthTag.Default + 13,

        // Starlight - start
        CyberspaceOverlays = DrawDepthTag.Default + 14,
        /// <summary>
        ///     Objects that exist in cyberspace and should render above the StationAI dark overlay (ZIndex 14).
        ///     Assign this to any entity that must be visible to the AI regardless of camera coverage.
        /// </summary>
        CyberspaceObjects = DrawDepthTag.Default + 100,
        // Starlight - end
    }
}
