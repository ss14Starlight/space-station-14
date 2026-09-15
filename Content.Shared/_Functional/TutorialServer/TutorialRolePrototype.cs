using System.Numerics;
using Content.Shared.AlertLevel;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Research.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Defines a selectable tutorial package for the Functional Tutorial Server.
/// </summary>
[Prototype] //Tutorial: drop redundant type (RA0042)
public sealed partial class TutorialRolePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Crew job to outfit as, if any.
    /// </summary>
    [DataField]
    public ProtoId<JobPrototype>? Job;

    /// <summary>
    /// Antagonist package id (matches <see cref="AntagPrototype"/>), if any.
    /// </summary>
    [DataField]
    public ProtoId<AntagPrototype>? Antag;

    /// <summary>
    /// When true, picker shows the role greyed with an incomplete marker.
    /// </summary>
    [DataField]
    public bool Stub = true;

    /// <summary>
    /// When true, this tutorial stays on the picker while
    /// <see cref="TutorialCVars.LiveTutorials"/> is enabled. Roles without this flag
    /// are hidden on the live host and shown with a stub prefix in development.
    /// </summary>
    [DataField]
    public bool LiveTutorial;

    /// <summary>
    /// When true, after the private map loads: force APC receivers to not need power.
    /// TEMPORARY: atmos freeze is currently skipped globally (odd behavior with fill-once /
    /// no-LINDA); see <c>TutorialMapSystem.FreezeAtmosInSimplifiedEnvironment</c>. Leave false
    /// for engineering/cargo tutorials that teach live power, spacing, or EVA.
    /// </summary>
    [DataField]
    public bool SimplifiedEnvironment;

    /// <summary>
    /// Map/grid path to load when <see cref="Room"/> is unset (stubs / legacy).
    /// </summary>
    [DataField]
    public ResPath Map = new("/Maps/_Functional/TutorialServer/StubPractice.yml");

    /// <summary>
    /// When set, loads/builds a single-room template and stamps N identical copies
    /// (goal-driven) with gated doors between them. Takes priority over <see cref="Room"/>.
    /// </summary>
    [DataField]
    public ProtoId<TutorialRoomTemplatePrototype>? RoomTemplate;

    /// <summary>
    /// Last-resort procedural room style when <see cref="RoomTemplate"/> is unset
    /// (or its map crop is missing). Builds one chamber then stamps copies the same way.
    /// </summary>
    [DataField]
    public ProtoId<TutorialRoomPrototype>? Room;

    /// <summary>
    /// When set, builds a shuttle + dock platform arena (takes priority over <see cref="Room"/>).
    /// </summary>
    [DataField]
    public ProtoId<TutorialShuttleArenaPrototype>? ShuttleArena;

    /// <summary>
    /// When set, builds a salvage bay + debris arena (takes priority over <see cref="Room"/>).
    /// </summary>
    [DataField]
    public ProtoId<TutorialSalvageArenaPrototype>? SalvageArena;

    /// <summary>
    /// When set, builds a Space Dragon prey arena (cargo-bay box + space spawn).
    /// Takes priority over <see cref="Room"/> after shuttle/salvage arenas.
    /// </summary>
    [DataField]
    public ProtoId<TutorialDragonArenaPrototype>? DragonArena;

    /// <summary>
    /// When true, builds the floating Syndicate outpost spawn lounge + chem lab fragment
    /// (takes priority over <see cref="Room"/> / <see cref="Map"/>, after shuttle/salvage arenas).
    /// </summary>
    [DataField]
    public bool NukeopsOutpost;

    /// <summary>
    /// When set, spawns this entity prototype as the player body instead of a humanoid job spawn
    /// (e.g. <c>XenoborgEngi</c>, <c>MothershipCore</c>).
    /// </summary>
    [DataField]
    public EntProtoId? SpawnEntity;

    /// <summary>
    /// Optional antag/job starting gear equipped after a gearless humanoid spawn.
    /// When set, Passenger/job loadouts are skipped in favor of this gear plus <see cref="RoleLoadout"/>.
    /// </summary>
    [DataField]
    public ProtoId<StartingGearPrototype>? StartingGear;

    /// <summary>
    /// Optional role loadout applied with <see cref="StartingGear"/> (e.g. <c>RoleSurvivalNukie</c>).
    /// </summary>
    [DataField]
    public ProtoId<RoleLoadoutPrototype>? RoleLoadout;

    /// <summary>
    /// Optional guidebook entry id for cross-linking.
    /// </summary>
    [DataField]
    public string? Guidebook;

    /// <summary>
    /// Plain-text objective prototype ids to add for Character UI (e.g. Traitor placeholders).
    /// </summary>
    [DataField]
    public List<EntProtoId> PlaceholderObjectives = new();

    /// <summary>
    /// Legacy flat steps (used when <see cref="Goals"/> is empty, mainly stubs).
    /// </summary>
    [DataField]
    public List<TutorialStepData> Steps = new();

    /// <summary>
    /// Multi-goal curriculum. When non-empty, replaces <see cref="Steps"/>.
    /// </summary>
    [DataField]
    public List<TutorialGoalData> Goals = new();

    /// <summary>
    /// Entities spawned on the private map after load (vendors, machines, props, markers).
    /// </summary>
    [DataField]
    public List<TutorialPracticeSpawn> PracticeSpawns = new();

    /// <summary>
    /// Optional offset from the chamber / zone-origin spawn point for the player body.
    /// Use when the crop center is outside the practice room (e.g. Command crop centers on Cap).
    /// </summary>
    [DataField]
    public Vector2 SpawnOffset;

    /// <summary>
    /// Offset from the chamber / zone-origin spawn point for the soft-following mentor.
    /// Defaults beside the player at <c>(1.2, 0)</c>.
    /// </summary>
    [DataField]
    public Vector2 MentorSpawnOffset = new(1.2f, 0f);

    /// <summary>
    /// Display name override locale id. Falls back to job/antag name.
    /// </summary>
    [DataField]
    public string? Name;

    /// <summary>
    /// Department grouping key for the picker UI.
    /// </summary>
    [DataField]
    public string Category = "Misc";

    /// <summary>
    /// Optional indented sub-heading under <see cref="Category"/> (e.g. BPL14 / Starlight).
    /// </summary>
    [DataField]
    public string? SubCategory;

    /// <summary>
    /// When true, the tutorial guide Bound UI opens as soon as the tablet is given.
    /// When false, open is deferred until chamber-pad check-in after the opening goal
    /// (never synchronously from a UseInHand that ends that goal — e.g. Passenger drink),
    /// or until a curriculum step force-opens it (e.g. Cargo Tech controls).
    /// </summary>
    [DataField]
    public bool AutoOpenGuide = true;

    /// <summary>
    /// Display name for the mentor (e.g. Urist McMalpractice). Used for single-grid coaches
    /// and hybrid travel roles that also spawn a stationary mentor.
    /// </summary>
    [DataField]
    public string? MentorName;

    /// <summary>
    /// When true with a travel coach, also spawn a mentor body (e.g. bay QM briefing).
    /// </summary>
    [DataField]
    public bool SpawnStationaryMentor;

    /// <summary>
    /// When false, the mentor does not HTN-follow or catch-up/snap to the player.
    /// </summary>
    [DataField]
    public bool MentorFollows = true;

    /// <summary>
    /// Entity prototype spawned as the mentor body. Falls back to the built-in
    /// <c>TutorialMentor</c> humanoid when unset.
    /// </summary>
    [DataField]
    public EntProtoId? MentorEntity;

    /// <summary>
    /// How the mentor keeps up with the player. <see cref="TutorialMentorMode.Walk"/> uses the
    /// HTN soft-follow; <see cref="TutorialMentorMode.Holopad"/> re-projects the mentor at the
    /// <see cref="TutorialHoloPointComponent"/> of the room the player is currently in.
    /// </summary>
    [DataField]
    public TutorialMentorMode MentorMode = TutorialMentorMode.Walk;

    /// <summary>
    /// Sort key inside <see cref="Category"/>, lowest first, ties falling back to display name. In
    /// "Start Here" the reading order is the teaching order and must not be alphabetical luck.
    /// </summary>
    [DataField]
    public int PickerOrder;

    /// <summary>
    /// Whether entering a chamber inserts a "stand on the glowing pad" check-in first. Worth it
    /// where a bench of machinery needs the player parked somewhere known, an errand where not.
    /// </summary>
    [DataField]
    public bool ChamberEntryPads = true;

    /// <summary>
    /// Species that cannot take this tutorial, shown greyed in the picker. For curricula a species
    /// arrives having already satisfied, e.g. Vox and their tank harness in the survival chamber.
    /// </summary>
    [DataField]
    public List<ProtoId<SpeciesPrototype>> BlockedSpecies = new();
}

/// <summary>
/// How a tutorial mentor accompanies the player between rooms.
/// </summary>
[Serializable, NetSerializable]
public enum TutorialMentorMode : byte
{
    /// <summary>HTN soft-follow (see <c>TutorialMentorFollowSystem</c>).</summary>
    Walk,

    /// <summary>Re-projected at each room's holopad; never physically moves.</summary>
    Holopad,

    /// <summary>
    /// Walks to the <see cref="TutorialWalkPointComponent"/> of the room the curriculum is in and
    /// waits there for the player (see <c>TutorialLeadMentorSystem</c>). The inverse of
    /// <see cref="Walk"/>: the player follows the coach rather than the coach trailing the player,
    /// which is what a mentor showing somebody around a station actually does.
    /// </summary>
    Lead,
}

/// <summary>
/// Posture a player must be in for a posture-qualified sub-goal to complete.
/// </summary>
[Serializable, NetSerializable]
public enum TutorialPosture : byte
{
    /// <summary>No posture requirement.</summary>
    Any,

    /// <summary>Upright and sprinting or walking.</summary>
    Standing,

    /// <summary>Upright with the walk modifier held (not sprinting).</summary>
    Walking,

    /// <summary>Knocked down / crawling.</summary>
    Crawling,
}

[DataDefinition]
public sealed partial class TutorialGoalData
{
    [DataField(required: true)]
    public string Id = string.Empty;

    /// <summary>
    /// Locale id for the goal title shown in the HUD.
    /// </summary>
    [DataField(required: true)]
    public string Title = string.Empty;

    /// <summary>
    /// When set, advancing into this goal unlocks the gate into that chamber and may
    /// inject a glowing-pad check-in. Prefer keeping early goals in chamber 0 and only
    /// setting this when a new room is actually required (hazard isolation, pry exit).
    /// </summary>
    [DataField]
    public int? EnterRoom;

    [DataField(required: true)]
    public List<TutorialSubGoalData> SubGoals = new();
}

[DataDefinition]
public sealed partial class TutorialSubGoalData
{
    [DataField(required: true)]
    public string Id = string.Empty;

    /// <summary>
    /// Locale id for sub-goal prompt text.
    /// </summary>
    [DataField(required: true)]
    public string Text = string.Empty;

    /// <summary>
    /// Optional locale id for a short actionable hint while waiting on a sensor.
    /// </summary>
    [DataField]
    public string? Hint;

    /// <summary>
    /// Optional locale id for an extra stuck tip shown via the prompt Hint button.
    /// </summary>
    [DataField]
    public string? StuckHint;

    /// <summary>
    /// Optional locale id for the on-screen control hint banner. This is the ONLY channel that
    /// should mention keys, and it should say nothing else — e.g.
    /// <c>Use [keybind="MoveUp"][keybind="MoveLeft"][keybind="MoveDown"][keybind="MoveRight"] to move.</c>
    /// Markup is resolved client-side, so the player sees their own bindings.
    /// Omitting it falls the banner back to <see cref="Text"/>, so the corner is never blank.
    /// </summary>
    [DataField]
    public string? ControlHint;

    /// <summary>
    /// When true, no on-screen control-hint banner is shown for this sub-goal (tablet-only beats).
    /// </summary>
    [DataField]
    public bool SuppressControlHint;

    /// <summary>
    /// Posture the player must hold for <see cref="TutorialStepComplete.ReachMarker"/> to count.
    /// Lets one marker beat teach "walk there" or "crawl there" without a new completion kind.
    /// </summary>
    [DataField]
    public TutorialPosture Posture = TutorialPosture.Any;

    /// <summary>
    /// When set on an <see cref="TutorialStepComplete.Acknowledge"/> sub-goal, the step advances
    /// on its own this many seconds after the coach speaks. Used for narration beats that land
    /// before the player has been taught to click anything.
    /// </summary>
    [DataField]
    public float? AutoAdvanceSeconds;

    /// <summary>
    /// Spoken by the coach when the player fails this drill: breaking its <see cref="Posture"/>,
    /// or reaching <see cref="RetryMarker"/> without having satisfied the sub-goal at all.
    /// </summary>
    [DataField]
    public LocId? RetryLine;

    /// <summary>
    /// Keeps a leading coach standing where he is for the length of this beat, instead of setting
    /// off for the next section's walk point the moment the goal changes.
    /// </summary>
    /// <remarks>
    /// A coach who leads walks on goal boundaries, which is usually right and occasionally ruins
    /// the beat: he opens the maintenance door the player was asked to open, or turns his back
    /// while they are still climbing out of a disposal unit. Set this on the beats he should watch
    /// rather than walk through.
    /// </remarks>
    [DataField]
    public bool MentorHolds;

    /// <summary>
    /// Marker that counts as failing this sub-goal if the player reaches it while the sub-goal is
    /// still current. Catches the player who walks the length of a drill without ever engaging
    /// with it, which no completion condition can detect on its own.
    /// </summary>
    [DataField]
    public string? RetryMarker;

    /// <summary>
    /// Marker the player is returned to when the drill is failed, so they retry it from the top.
    /// </summary>
    [DataField]
    public string? RetryReturnMarker;

    [DataField]
    public TutorialStepComplete Complete = TutorialStepComplete.Acknowledge;

    /// <summary>
    /// Tag used for InteractTag / InteractTargetTag / HoldTag.
    /// </summary>
    [DataField]
    public string? Tag;

    /// <summary>
    /// Entity prototype for HoldItem / ObtainItem / UseInHand / HasAction /
    /// ActionUsed / MapHasEntity matching.
    /// </summary>
    [DataField]
    public EntProtoId? Entity;

    /// <summary>
    /// Marker id for ReachMarker (matches <see cref="TutorialStepMarkerComponent.MarkerId"/>),
    /// or dock-station id for DockShuttle / UndockShuttle (matches <see cref="TutorialDockStationComponent.StationId"/>),
    /// or puddle marker id for <see cref="TutorialStepComplete.PuddleCleared"/>.
    /// </summary>
    [DataField]
    public string? Marker;

    /// <summary>
    /// How close to <see cref="Marker"/> counts, in tiles. Only read by
    /// <see cref="TutorialStepComplete.EntityAtMarker"/>, whose default of a tile and a half suits
    /// "put it on the counter" but is wider than a tile — set it tight for a drill where the
    /// neighbouring tile already holds something that would match.
    /// </summary>
    [DataField]
    public float? MarkerRange;

    /// <summary>
    /// Equipment slot qualifier: on <see cref="TutorialStepComplete.StowItem"/> the item must be in
    /// that slot rather than anywhere on the body, on
    /// <see cref="TutorialStepComplete.StorageOpened"/> it is where the opened storage is worn.
    /// </summary>
    [DataField]
    public string? Slot;

    /// <summary>
    /// Component an item must carry to satisfy a possession sensor, for drills that must not name a
    /// prototype: a nitrogen and an oxygen breather carry different tanks, both <c>GasTank</c>.
    /// </summary>
    [DataField]
    public string? Component;

    /// <summary>Anchor state <see cref="TutorialStepComplete.TargetAnchored"/> waits for.</summary>
    [DataField]
    public bool Anchored;

    /// <summary>
    /// Reagent prototype for <see cref="TutorialStepComplete.SolutionContains"/>.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    /// <summary>
    /// Minimum reagent units for <see cref="TutorialStepComplete.SolutionContains"/> (default 1).
    /// </summary>
    [DataField]
    public FixedPoint2 MinAmount = 1;

    /// <summary>
    /// Maximum total damage for <see cref="TutorialStepComplete.PracticeMobDamageBelow"/> (default 0).
    /// </summary>
    [DataField]
    public float MaxDamage;

    /// <summary>
    /// Minimum count of matching entities for <see cref="TutorialStepComplete.MapHasEntity"/>
    /// or items inside a tagged container for <see cref="TutorialStepComplete.ContainerHasEntityCount"/> (default 1).
    /// </summary>
    [DataField]
    public int MinCount = 1;

    /// <summary>
    /// Job prototype for <see cref="TutorialStepComplete.IdCardHasJob"/>.
    /// </summary>
    [DataField]
    public ProtoId<JobPrototype>? Job;

    /// <summary>
    /// Technology prototype for <see cref="TutorialStepComplete.ResearchUnlocked"/>.
    /// </summary>
    [DataField]
    public ProtoId<TechnologyPrototype>? Technology;

    /// <summary>
    /// Alert level prototype id for <see cref="TutorialStepComplete.AlertLevelChanged"/>
    /// (e.g. <c>blue</c>). Defaults to blue when unset.
    /// </summary>
    [DataField]
    public string? AlertLevel; // Starlight edit - AlertLevelPrototype not in shared, just resolve this from string.
}

/// <summary>
/// Legacy flat step (stubs / backward compatibility).
/// </summary>
[DataDefinition]
public sealed partial class TutorialStepData
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public string Text = string.Empty;

    /// <summary>
    /// Optional locale id for a short actionable hint while waiting on a sensor.
    /// </summary>
    [DataField]
    public string? Hint;

    /// <summary>
    /// Optional locale id for an extra stuck tip shown via the prompt Hint button.
    /// </summary>
    [DataField]
    public string? StuckHint;

    [DataField]
    public TutorialStepComplete Complete = TutorialStepComplete.Acknowledge;

    [DataField]
    public string? Tag;

    [DataField]
    public EntProtoId? Entity;

    [DataField]
    public string? Marker;
}

[DataDefinition]
public sealed partial class TutorialPracticeSpawn
{
    [DataField(required: true)]
    public EntProtoId Id;

    /// <summary>
    /// Offset from the chamber center (see <see cref="Room"/>).
    /// </summary>
    [DataField]
    public Vector2 Offset = Vector2.Zero;

    /// <summary>
    /// Which chamber to place this entity in (0 = spawn / first goal room).
    /// Out-of-range values clamp to the last built chamber.
    /// </summary>
    [DataField]
    public int Room;

    /// <summary>
    /// If set, attaches a step marker with this id after spawning.
    /// </summary>
    [DataField]
    public string? Marker;

    /// <summary>
    /// Force ApcPowerReceiver.NeedsPower = false so machines work on isolated grids.
    /// </summary>
    [DataField]
    public bool AlwaysPowered = true;
}

[Serializable, NetSerializable]
public enum TutorialStepComplete : byte
{
    /// <summary>Player presses Continue on the HUD.</summary>
    Acknowledge,

    /// <summary>Player collides with / reaches a marker entity.</summary>
    ReachMarker,

    /// <summary>Player interacts using a held item that has <see cref="TutorialSubGoalData.Tag"/>.</summary>
    InteractTag,

    /// <summary>Player interacts with a world target that has <see cref="TutorialSubGoalData.Tag"/>.</summary>
    InteractTargetTag,

    /// <summary>
    /// Player interacts with a world target that has <see cref="TutorialSubGoalData.Tag"/>
    /// while holding an item matching <see cref="TutorialSubGoalData.Entity"/>.
    /// </summary>
    InteractTargetHolding,

    /// <summary>Player holds an item matching <see cref="TutorialSubGoalData.Entity"/>.</summary>
    HoldItem,

    /// <summary>Player holds an item with <see cref="TutorialSubGoalData.Tag"/>.</summary>
    HoldTag,

    /// <summary>Player has the entity in hands or inventory.</summary>
    ObtainItem,

    /// <summary>Player uses the matching held item in-hand.</summary>
    UseInHand,

    /// <summary>
    /// Player dropped a matching <see cref="TutorialSubGoalData.Entity"/> to the world
    /// (not stowed into inventory).
    /// </summary>
    DropItem,

    /// <summary>
    /// Player has the entity in an inventory/storage slot (not currently held in hands).
    /// </summary>
    StowItem,

    /// <summary>Player is piloting a shuttle (has PilotComponent).</summary>
    PilotShuttle,

    /// <summary>Player is providing shuttle throttle / strafe / rotate input while piloting.</summary>
    ShuttleThrottle,

    /// <summary>
    /// Player's flyable shuttle is within approach range of a
    /// <see cref="TutorialDockStationComponent"/> whose StationId matches Marker.
    /// </summary>
    NearDockStation,

    /// <summary>Player's grid undocks from another grid.</summary>
    UndockShuttle,

    /// <summary>Player's grid docks to another grid.</summary>
    DockShuttle,

    /// <summary>Player spawned a tutorial anomaly via the spawn pad.</summary>
    SpawnAnomaly,

    /// <summary>Player scanned an anomaly (held/inventory scanner has ScannedAnomaly set).</summary>
    ScanAnomaly,

    /// <summary>Scanned anomaly stability is at or below the tutorial stabilize threshold.</summary>
    StabilizeAnomaly,

    /// <summary>Anomaly on the player's map shut down without going supercritical.</summary>
    RemoveAnomaly,

    /// <summary>Held/inventory solution contains <see cref="TutorialSubGoalData.Reagent"/> ≥ MinAmount.</summary>
    SolutionContains,

    /// <summary>Practice puddle with matching MarkerId is gone or empty on the player's map.</summary>
    PuddleCleared,

    /// <summary>Player cuffed a <see cref="TutorialPracticeMobComponent"/> on their map.</summary>
    PracticeMobCuffed,

    /// <summary>
    /// Non-dead practice mobs on the map have total damage at or below MaxDamage.
    /// Dead practice mobs are ignored so a corpse can coexist with a heal drill.
    /// </summary>
    PracticeMobDamageBelow,

    /// <summary>AME controller on the map has fuel and is injecting.</summary>
    AmeInjecting,

    /// <summary>Player harvested produce from a tutorial hydro tray (optionally matching Entity).</summary>
    HydroHarvest,

    /// <summary>
    /// At least <see cref="TutorialSubGoalData.MinCount"/> entities matching
    /// <see cref="TutorialSubGoalData.Entity"/> exist on the player's map
    /// (used for placed cables, built SMES, etc.).
    /// </summary>
    MapHasEntity,

    /// <summary>
    /// A tagged practice entity on the map has its wires panel open
    /// (<see cref="TutorialSubGoalData.Tag"/>).
    /// </summary>
    WiresPanelOpen,

    /// <summary>A practice mob on the map was hit by a cream pie.</summary>
    PracticeMobCreamPied,

    /// <summary>
    /// A practice mob on the map is buckled to a rollerbed tagged
    /// <c>TutorialRollerBed</c>.
    /// </summary>
    PracticeMobBuckled,

    /// <summary>
    /// Player opened the Starlight surgery Bound UI on a tutorial patient.
    /// </summary>
    StarlightSurgeryUiOpened,

    /// <summary>
    /// A tutorial Starlight surgery patient on the map has an implanted eye cybernetic.
    /// </summary>
    StarlightSurgeryEyeImplanted,

    /// <summary>
    /// Player opened the CyberMed analyzer Bound UI on a tutorial patient.
    /// </summary>
    CyberMedSurgeryUiOpened,

    /// <summary>
    /// A tutorial BPL CyberMed surgery patient finished the example implant + close path.
    /// </summary>
    CyberMedSurgeryComplete,

    /// <summary>
    /// An ID card on the player's map has <see cref="TutorialSubGoalData.Job"/> written.
    /// </summary>
    IdCardHasJob,

    /// <summary>
    /// A tagged storage/locker on the map contains at least <see cref="TutorialSubGoalData.MinCount"/> items.
    /// </summary>
    ContainerHasEntityCount,

    /// <summary>
    /// Player fed an item into a tagged tutorial recycler on their map.
    /// </summary>
    RecyclerProcessed,

    /// <summary>A practice mob on the map was slipped (soap/peel).</summary>
    PracticeMobSlipped,

    /// <summary>
    /// At least <see cref="TutorialSubGoalData.MinCount"/> entities were sold via cargo pallet sale
    /// on the player's map. When <see cref="TutorialSubGoalData.Tag"/> is set, only sold entities
    /// with that tag count.
    /// </summary>
    CargoSold,

    /// <summary>
    /// A thermo-electric generator on the player's map has <c>LastGeneration &gt; 0</c>.
    /// Curriculum should require a prior TEG interact sub-goal so this cannot idle-complete.
    /// </summary>
    TegProducingPower,

    /// <summary>
    /// A technology database on the player's map has unlocked
    /// <see cref="TutorialSubGoalData.Technology"/>.
    /// </summary>
    ResearchUnlocked,

    /// <summary>
    /// A tagged tutorial lathe on the player's map started printing a recipe whose result
    /// matches <see cref="TutorialSubGoalData.Entity"/>.
    /// </summary>
    LathePrinted,

    /// <summary>
    /// A nuclear bomb on the player's map is in the armed state.
    /// </summary>
    NukeArmed,

    /// <summary>
    /// Player successfully completed a tutorial war declaration (WarReady).
    /// </summary>
    WarDeclared,

    /// <summary>
    /// Player entity has <c>ZombieComponent</c> (e.g. after Turn Undead).
    /// </summary>
    PlayerIsZombie,

    /// <summary>
    /// At least <see cref="TutorialSubGoalData.MinCount"/> practice mobs on the map have
    /// <c>PendingZombieComponent</c> or <c>ZombieComponent</c>.
    /// </summary>
    PracticeMobInfected,

    /// <summary>
    /// At least <see cref="TutorialSubGoalData.MinCount"/> practice mobs on the map have
    /// <c>RevolutionaryComponent</c> (converted by a Head Revolutionary flash).
    /// </summary>
    PracticeMobConverted,

    /// <summary>
    /// The player's own wires panel is open (used for cyborg maintenance / emag setup).
    /// </summary>
    PlayerWiresPanelOpen,

    /// <summary>
    /// The player's <c>SiliconLawProviderComponent.Subverted</c> is true (emagged / ion-stormed).
    /// </summary>
    SiliconSubverted,

    /// <summary>
    /// A practice mob on the map is dead (<c>MobState.Dead</c>).
    /// </summary>
    PracticeMobDead,

    /// <summary>
    /// A practice mob on the map left Dead for Critical or Alive (e.g. successful defibrillation).
    /// </summary>
    PracticeMobRevived,

    /// <summary>
    /// Player successfully finished a changeling devour (<c>ChangelingDevouredEvent</c>).
    /// </summary>
    ChangelingDevoured,

    /// <summary>
    /// Player used Extract DNA sting on a target.
    /// </summary>
    ChangelingStung,

    /// <summary>
    /// Player owns an action matching <see cref="TutorialSubGoalData.Entity"/> (e.g. ArmBlade).
    /// </summary>
    HasAction,

    /// <summary>
    /// Vampire <c>TotalBlood</c> is at least <see cref="TutorialSubGoalData.MinCount"/>.
    /// </summary>
    VampireBloodAbove,

    /// <summary>
    /// Vampire has chosen a class path (<c>ChosenClassId</c> set).
    /// </summary>
    VampireClassChosen,

    /// <summary>
    /// Vampire fangs are extended.
    /// </summary>
    VampireFangsExtended,

    /// <summary>
    /// A cargo order was approved on the player's station (approve console path).
    /// </summary>
    CargoOrderApproved,

    /// <summary>
    /// A bounty-labeled crate was sold and fulfilled on the player's map.
    /// </summary>
    CargoBountyFulfilled,

    /// <summary>
    /// A practice mob on the map was stunned/knocked down (or stun tool InteractUsing).
    /// </summary>
    PracticeMobStunned,

    /// <summary>
    /// A tagged tutorial brig timer on the map has an active signal timer.
    /// </summary>
    BrigTimerStarted,

    /// <summary>
    /// Participant spent Telecrystal (or bought something) on their PDA/implant uplink store.
    /// </summary>
    StorePurchased,

    /// <summary>
    /// Station alert level changed to <see cref="TutorialSubGoalData.AlertLevel"/> (default blue).
    /// </summary>
    AlertLevelChanged,

    /// <summary>
    /// A thieving beacon on the player's map is linked (StealArea OwnerCount &gt; 0).
    /// Unfolding the beacon as a thief auto-links it to their mind.
    /// </summary>
    ThiefBeaconLinked,

    /// <summary>
    /// Player successfully used an action matching <see cref="TutorialSubGoalData.Entity"/>
    /// (fires after the action event is handled).
    /// </summary>
    ActionUsed,

    /// <summary>
    /// Player finished devouring a humanoid (Devour do-after completed on a
    /// <c>HumanoidProfile</c> target — grants Ichor healing for space dragons).
    /// </summary>
    DragonDevoured,

    /// <summary>
    /// Player selected a cyborg chassis type. When <see cref="TutorialSubGoalData.Marker"/>
    /// is set, it must match the selected <c>borgType</c> prototype id (e.g. <c>generic</c>).
    /// </summary>
    BorgTypeSelected,

    /// <summary>
    /// Player's active borg module matches <see cref="TutorialSubGoalData.Entity"/>
    /// (must differ from the chassis's initially auto-selected module — use after a tip).
    /// </summary>
    BorgModuleSelected,

    /// <summary>
    /// Player is wearing <see cref="TutorialSubGoalData.Entity"/> in an inventory clothing slot.
    /// </summary>
    WearItem,

    /// <summary>
    /// A tagged practice entity on the map has power disabled / is unpowered
    /// (<see cref="TutorialSubGoalData.Tag"/>).
    /// </summary>
    TargetPowerDisabled,

    /// <summary>
    /// A tagged door on the map is fully open (<see cref="TutorialSubGoalData.Tag"/>).
    /// </summary>
    TargetDoorOpen,

    /// <summary>
    /// A tagged entity on the map has all power wires cut (<see cref="TutorialSubGoalData.Tag"/>).
    /// </summary>
    PowerWiresCut,

    /// <summary>
    /// No wire on a tagged entity is cut (<see cref="TutorialSubGoalData.Tag"/>). Stronger than
    /// <see cref="TargetPowered"/> on purpose: a door can be live with its bolt wire still cut, and
    /// a cut wire cannot be pulsed, so the player is left holding a multitool at a wire that will
    /// never answer with nothing on screen to say why.
    /// </summary>
    TargetWiresIntact,

    /// <summary>
    /// A tagged entity on the map is powered again (<see cref="TutorialSubGoalData.Tag"/>). The
    /// counterpart to <see cref="PowerWiresCut"/>, for the half of a hack that puts a wire back.
    /// Only means anything on a beat that follows one which took the power off, since anything
    /// still plugged in satisfies it on the frame it becomes current.
    /// </summary>
    TargetPowered,

    /// <summary>
    /// A cargo order was added on the player's map (purchase / request path).
    /// </summary>
    CargoOrderAdded,

    /// <summary>
    /// Player is pulling an entity with <see cref="TutorialSubGoalData.Tag"/>.
    /// </summary>
    PullTag,

    /// <summary>Player pressed a directional movement key.</summary>
    PlayerMoved,

    /// <summary>Player moved with the walk modifier engaged (not sprinting).</summary>
    PlayerWalking,

    /// <summary>Player is knocked down / crawling.</summary>
    PlayerCrawling,

    /// <summary>Player stood back up after crawling.</summary>
    PlayerStanding,

    /// <summary>
    /// Player climbed onto something, optionally matching <see cref="TutorialSubGoalData.Tag"/>.
    /// </summary>
    PlayerClimbed,

    /// <summary>
    /// Player buckled into a strap, optionally matching <see cref="TutorialSubGoalData.Tag"/>.
    /// </summary>
    PlayerBuckled,

    /// <summary>Player unbuckled themselves.</summary>
    PlayerUnbuckled,

    /// <summary>
    /// Player pointed at something, optionally matching <see cref="TutorialSubGoalData.Tag"/>.
    /// </summary>
    PlayerPointed,

    /// <summary>Player rotated their camera away from the default orientation.</summary>
    CameraRotated,

    /// <summary>Player returned a rotated camera to the default orientation.</summary>
    CameraResetDone,

    /// <summary>Player made their other hand the active one.</summary>
    PlayerSwappedHands,

    /// <summary>
    /// Player examined an entity carrying <see cref="TutorialSubGoalData.Tag"/>. The target also
    /// needs <c>TutorialSensorTarget</c>, so this is not a subscription on every examine.
    /// </summary>
    ExamineTag,

    /// <summary>
    /// Player used a tagged world target with the activate key specifically. Unlike
    /// <see cref="InteractTargetTag"/> a plain click does not count.
    /// </summary>
    ActivateInWorldTag,

    /// <summary>Player threw a matching <see cref="TutorialSubGoalData.Entity"/>, hit or miss.</summary>
    ThrewItem,

    /// <summary>Player opened a storage UI, matched by tag, prototype or worn slot.</summary>
    StorageOpened,

    /// <summary>
    /// A tagged disposal unit is engaged. Flush is its top-priority alt verb, so this is proof the
    /// player used the alternate action.
    /// </summary>
    DisposalEngaged,

    /// <summary>Player has a breath tool equipped, read off <c>Internals</c> so any species passes.</summary>
    BreathToolEquipped,

    /// <summary>Player has internals connected to a gas tank.</summary>
    InternalsOn,

    /// <summary>
    /// The item in the player's <i>active</i> hand matches the spec, where <see cref="HoldItem"/>
    /// accepts either hand. For drills teaching that the active hand is the one that acts.
    /// </summary>
    ActiveHandItem,

    /// <summary>
    /// A tagged entity's <c>Transform.Anchored</c> matches <see cref="TutorialSubGoalData.Anchored"/>.
    /// The finished bolt, not the click, so an interrupted do-after cannot advance the drill.
    /// </summary>
    TargetAnchored,

    /// <summary>Nothing tagged is left on the map: for drills that end by consuming their target.</summary>
    TargetAbsent,

    /// <summary>Every one of the player's hands is empty.</summary>
    HandsEmpty,

    /// <summary>
    /// The player's active hand holds nothing. Unlike <see cref="HandsEmpty"/>, the other hand
    /// may still be full. For drills that need a free hand to pick something up.
    /// </summary>
    ActiveHandEmpty,

    /// <summary>
    /// A tagged entity is parked on <see cref="TutorialSubGoalData.Marker"/> and nobody is pulling
    /// it. The release matters: letting go is a separate control from taking hold.
    /// </summary>
    TargetParkedAtMarker,

    /// <summary>Player's internals are disconnected; the counterpart to <see cref="InternalsOn"/>.</summary>
    InternalsOff,

    /// <summary>
    /// A tagged door refused the player for want of access. Completes on the failure, because
    /// being told no by a door is the thing being taught.
    /// </summary>
    DoorAccessDenied,

    /// <summary>Player took a shock. Taught by consequence, so the drill is to get hurt once.</summary>
    PlayerShocked,

    /// <summary>
    /// A tagged door's bolts are up. The state rather than the pulse that raised them, so a player
    /// who gets there some other way still passes. Named for the state and not the verb on
    /// purpose: crew say "drop the bolts" for locking a door, and this is the opposite of that.
    /// </summary>
    DoorBoltsRaised,

    /// <summary>Player opened the construction menu.</summary>
    ConstructionMenuOpened,

    /// <summary>Player is inside a tagged disposal unit, before the flush that sends them off.</summary>
    PlayerInDisposal,

    /// <summary>A tagged vending machine has had its contraband stock unlocked.</summary>
    VendorContrabandUnlocked,

    /// <summary>
    /// Something matching the spec is resting at <see cref="TutorialSubGoalData.Marker"/> and is
    /// not in anyone's hands. For "put it down there", which no possession sensor can express.
    /// </summary>
    EntityAtMarker,

    /// <summary>
    /// Player hugged their tutorial mentor (empty-hand <c>InteractionPopup</c> success).
    /// Mentor click handling must not mark the interact handled before the popup runs.
    /// </summary>
    InteractMentor,
}
