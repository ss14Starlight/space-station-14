using Robust.Shared.Audio;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Cluwne;

[RegisterComponent]
[NetworkedComponent]
public sealed partial class CluwneComponent : Component
{
    /// <summary>
    /// timings for giggles and knocks.
    /// </summary>
    [DataField]
    public TimeSpan DamageGiggleCooldown = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Amount of genetic damage dealt when they revert
    /// </summary>
    [DataField]
    public DamageSpecifier RevertDamage = new()
    {
        DamageDict = new()
        {
            { "Genetic", 300.0 },
        },
    };

    /// <summary>
    /// Chance that the Cluwne will be knocked over and paralyzed.
    /// </summary>
    [DataField]
    public float KnockChance = 0.05f;

    /// <summary>
    /// Chance that the Cluwne will randomly giggle
    /// </summary>
    [DataField]
    public float GiggleRandomChance = 0.1f;

    /// <summary>
    /// Enable random emoting?
    /// </summary>
    [DataField]
    public bool RandomEmote = true;

    /// <summary>
    /// Emote sound collection that the Cluwne should use.
    /// </summary>
    [DataField("emoteId")]
    public ProtoId<EmoteSoundsPrototype>? EmoteSoundsId = "Cluwne";

    /// <summary>
    /// Emote to use for the Cluwne Giggling
    /// </summary>
    [DataField]
    public ProtoId<AutoEmotePrototype>? AutoEmoteId = "CluwneGiggle";

    /// <summary>
    /// Message to popup when the Cluwne is transformed
    /// </summary>
    [DataField]
    public LocId TransformMessage = "cluwne-transform";

    /// <summary>
    /// Name prefix for the Cluwne.
    /// Example "Urist McHuman" will be "Cluwned Urist McHuman"
    /// </summary>
    [DataField]
    public LocId NamePrefix = "cluwne-name-prefix";

    /// <summary>
    /// Outfit ID that the cluwne will spawn with.
    /// </summary>
    [DataField]
    public ProtoId<StartingGearPrototype> OutfitId = "CluwneGear";

    // Starlight start
    /// <summary>
    /// Should be true if this is a cluwne.
    /// </summary>
    [DataField]
    public bool IsCluwne = true;

    /// <summary>
    /// The autoemote sound to play.
    /// </summary>
    [DataField]
    public string AutoEmoteSound = "CluwneGiggle";

    /// <summary>
    /// Portal proto id.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntProtoId Portal = "PortalGreeny";
    // Starlight end

    /// <summary>
    /// Amount of time cluwne is paralyzed for when falling over.
    /// </summary>
    [DataField]
    public float ParalyzeTime = 2f;

    /// <summary>
    /// Sound specifiers for honk and knock.
    /// </summary>
    [DataField]
    public SoundSpecifier SpawnSound = new SoundPathSpecifier("/Audio/Items/bikehorn.ogg");

    /// <summary>
    /// Emote to use for the cluwne giggling
    /// </summary>
    [DataField]
    public LocId GiggleEmote = "cluwne-giggle-emote";

    /// <summary>
    /// Sound to play when the Cluwne is knocked over and paralyzed
    /// </summary>
    [DataField]
    public SoundSpecifier KnockSound = new SoundPathSpecifier("/Audio/Items/airhorn.ogg");

    /// <summary>
    /// Emote thats used when the cluwne getting knocked over
    /// </summary>
    [DataField]
    public LocId KnockEmote = "cluwne-knock-emote";


    //#region Starlight
    /// <summary>
    /// Sound that's played on conversion(?)
    /// </summary>
    [DataField]
    public SoundSpecifier CluwneSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/Magic/staff_animation.ogg");

    /// <summary>
    /// Portal sound for beast arrival.
    /// </summary>
    [DataField]
    public SoundSpecifier ArrivalSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    /// <summary>
    /// whether this cluwne is permanent and should be unremovable by bible thwacks
    /// </summary>
    [DataField]
    public bool Unremovable = false;

    //#endregion Starlight

}
