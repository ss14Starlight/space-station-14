using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Polymorph;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.FixedPoint;

namespace Content.Shared._Starlight.Antags.Vampires;

#region Basic Abilities

public sealed partial class VampireGlareActionEvent : InstantActionEvent
{
    /// <summary>
    /// The distance at which entities will be blinded.
    /// </summary>
    [DataField]
    public float Range = 1f;

    /// <summary>
    /// How much we need to apply stamina damage on entity in front of glare source
    /// </summary>
    [DataField]
    public float FrontStaminaDamage = 30f;

    /// <summary>
    /// How much we need to apply stamina damage on entity behind of glare source
    /// </summary>
    [DataField]
    public float BehindStaminaDamage = 30f;

    /// <summary>
    /// How much we need to apply stamina damage on entity which is located to the left or right of glare source
    /// </summary>
    [DataField]
    public float SideStaminaDamage = 40f;

    /// <summary>
    /// How much we need to apply additional stamina damage on entity in front of glare source.
    /// </summary>
    [DataField]
    public float DotStaminaDamage = 15f;

    /// <summary>
    /// How many seconds do we need to mute entity in front of glare source.
    /// </summary>
    [DataField]
    public TimeSpan MuteDuration = TimeSpan.FromSeconds(8);
}

public sealed partial class VampireRejuvenateIActionEvent : InstantActionEvent;

public sealed partial class VampireRejuvenateIIActionEvent : InstantActionEvent;

public sealed partial class VampireClassSelectActionEvent : InstantActionEvent;

public sealed partial class VampireToggleFangsActionEvent : InstantActionEvent;
public sealed partial class VampireLocateMindActionEvent : InstantActionEvent;

public sealed class VampireBloodDrankEvent : EntityEventArgs
{
    public EntityUid Target { get; }
    public float Amount { get; }

    public VampireBloodDrankEvent(EntityUid target, float amount)
    {
        Target = target;
        Amount = amount;
    }
}

public sealed class VampireFullPowerAchievedEvent : EntityEventArgs
{
}

/// <summary>
/// Raised locally on a vampire when their progression related blood values change
/// </summary>
public sealed class VampireProgressionChangedEvent : EntityEventArgs { }

#endregion

#region Hemomancer

// Vampiric Claws
public sealed partial class VampireHemomancerClawsActionEvent : InstantActionEvent;

// Blood Tendril
public sealed partial class VampireHemomancerTendrilsActionEvent : WorldTargetActionEvent
{
    [DataField]
    public EntProtoId TendrilsVisualPrototype = "VampireBloodTendrilVisual";

    [DataField]
    public EntProtoId TendrilsPuddlePrototype = "PuddleBlood";

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);

    [DataField]
    public float SlowMultiplier = 0.3f;

    [DataField]
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public FixedPoint2 ToxinDamage = FixedPoint2.New(33);

    [DataField]
    public bool SpawnVisuals = true;

    [DataField]
    public float PositionOffset = 0.5f;

    [DataField]
    public float TargetRange = 0.9f;

    [DataField]
    public TimeSpan VisualSpawnDelay = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan MinDelay = TimeSpan.Zero;

    [DataField]
    public TimeSpan MinSlowDuration = TimeSpan.FromSeconds(0.1);

    [DataField]
    public float MinSlowMultiplier = 0.05f;
}

// Blood Barrier
public sealed partial class VampireBloodBarrierActionEvent : WorldTargetActionEvent
{
    [DataField]
    public EntProtoId BarrierPrototype = "VampireBloodBarrier";

    [DataField]
    public int BarrierCount = 3;
}

// Blood Pool
public sealed partial class VampireSanguinePoolActionEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId EnterEffectPrototype = "VampireSanguinePoolOut";

    [DataField]
    public EntProtoId ExitEffectPrototype = "VampireSanguinePoolIn";

    [DataField]
    public ProtoId<PolymorphPrototype> PolymorphPrototype = "VampireSanguinePoolPolymorph";

    [DataField]
    public SoundSpecifier EnterSound = new SoundPathSpecifier("/Audio/_Starlight/Effects/vampire/enter_blood.ogg");

    [DataField]
    public SoundSpecifier ExitSound = new SoundPathSpecifier("/Audio/_Starlight/Effects/vampire/exit_blood.ogg");

    [DataField]
    public TimeSpan BloodDripInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(8);
}

// Blood Eruption
public sealed partial class VampireBloodEruptionActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_Starlight/Effects/vampire/blooderruption.ogg");

    [DataField]
    public float Range = 10f;

    [DataField]
    public FixedPoint2 Damage = FixedPoint2.New(15);

    [DataField]
    public float TargetRange = 2f;
}

// The Blood Bringer's Rite
public sealed partial class VampireBloodBringersRiteActionEvent : InstantActionEvent
{
    [DataField]
    public float Range = 4f;

    [DataField]
    public FixedPoint2 Damage = FixedPoint2.New(5);

    [DataField]
    public float MaxTargetBlood = 10f;

    [DataField]
    public FixedPoint2 HealBrute = FixedPoint2.New(8);

    [DataField]
    public FixedPoint2 HealBurn = FixedPoint2.New(2);

    [DataField]
    public float HealStamina = 15f;

    [DataField]
    public TimeSpan ToggleInterval = TimeSpan.FromSeconds(2);

    [DataField]
    public int Cost = 10;
}

#endregion

#region Umbrae

// Cloak of Darkness
public sealed partial class VampireCloakOfDarknessActionEvent : InstantActionEvent;

// Shadow Snare
public sealed partial class VampireShadowSnareActionEvent : WorldTargetActionEvent
{
    [DataField]
    public EntProtoId SnarePrototype = "VampireShadowSnare";
}

// Soul Anchor
public sealed partial class VampireShadowAnchorActionEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId BeaconPrototype = "VampireShadowAnchorBeacon";

    /// <summary>
    /// DoAfter duration to place the anchor.
    /// </summary>
    [DataField]
    public TimeSpan PlaceDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Time window to return to the anchor. After this, return triggers automatically.
    /// </summary>
    [DataField]
    public TimeSpan AutoReturnDelay = TimeSpan.FromMinutes(2);
}

[Serializable, NetSerializable]
public sealed partial class VampireShadowAnchorDoAfterEvent : SimpleDoAfterEvent
{
    [DataField("coordinates", required: true)]
    public NetCoordinates TargetCoordinates;

    [DataField]
    public EntProtoId BeaconPrototype = "VampireShadowAnchorBeacon";

    [DataField]
    public int BloodCost;

    [DataField]
    public TimeSpan AutoReturnDelay;

    private VampireShadowAnchorDoAfterEvent()
    {
    }

    public VampireShadowAnchorDoAfterEvent(NetCoordinates coords, EntProtoId beaconPrototype, int bloodCost, TimeSpan autoReturnDelay)
    {
        TargetCoordinates = coords;
        BeaconPrototype = beaconPrototype;
        BloodCost = bloodCost;
        AutoReturnDelay = autoReturnDelay;
    }

    public override DoAfterEvent Clone() => this;
}

// Dark Passage
public sealed partial class VampireDarkPassageActionEvent : WorldTargetActionEvent
{
    [DataField]
    public EntProtoId MistInPrototype = "VampireDarkPassageMistIn";

    [DataField]
    public EntProtoId MistOutPrototype = "VampireDarkPassageMistOut";
    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");
}

// Extinguish
public sealed partial class VampireExtinguishActionEvent : InstantActionEvent
{
    [DataField]
    public float Radius = 4f;
}

// Shadow Boxing
public sealed partial class VampireShadowBoxingActionEvent : EntityTargetActionEvent
{
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(0.9);

    [DataField]
    public int BrutePerTick = 6;

    [DataField]
    public float Range = 4f;

    [DataField]
    public SoundSpecifier? HitSound;
}

[Serializable, NetSerializable]
public sealed class VampireShadowBoxingPunchEvent : EntityEventArgs
{
    public NetEntity Source { get; }
    public NetEntity Target { get; }

    public VampireShadowBoxingPunchEvent(NetEntity source, NetEntity target)
    {
        Source = source;
        Target = target;
    }
    [DataField]
    public TimeSpan PunchLifetime = TimeSpan.FromSeconds(0.33); 
    [DataField]
    public string EffectProto = "VampireShadowBoxingPunch";
}

// Eternal Darkness
public sealed partial class VampireEternalDarknessActionEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId AuraPrototype = "VampireEternalDarknessAura";

    [DataField]
    public int MaxTicks = 360;

    [DataField]
    public int BloodPerTick = 5;

    [DataField]
    public float FreezeRadius = 6f;

    [DataField]
    public float LightOffRadius = 4f;

    [DataField]
    public float TargetFreezeTemp = 233.15f;

    [DataField]
    public int TempDropInterval = 2;

    [DataField]
    public float TempDropPerInterval = 60f;
}

#endregion

#region Dantalion

public sealed partial class VampireEnthrallActionEvent : EntityTargetActionEvent
{
    /// <summary>
    ///     Channel duration, in seconds, before the target is enthralled
    /// </summary>
    [DataField]
    public TimeSpan ChannelTime = TimeSpan.FromSeconds(15);
}

[Serializable, NetSerializable]
public sealed partial class VampireDrinkBloodDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class VampireDevourDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public float BloodFullnessRestore;
}

[Serializable, NetSerializable]
public sealed partial class VampireEnthrallDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public int BloodCost;
}

public sealed partial class VampirePacifyActionEvent : EntityTargetActionEvent
{
    [DataField]
    public TimeSpan PacifyDuration = TimeSpan.FromSeconds(40);
}
        
public sealed partial class VampireSubspaceSwapActionEvent : EntityTargetActionEvent
{
    [DataField]
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public float SlowMultiplier = 0.4f;
    [DataField]
    public TimeSpan HysteriaDuration = TimeSpan.FromSeconds(15);
}

public sealed partial class VampireDecoyActionEvent : InstantActionEvent
{
    [DataField]
    public TimeSpan DecoyDuration = TimeSpan.FromSeconds(6);
    [DataField]
    public TimeSpan InvisibilityDuration = TimeSpan.FromSeconds(6);
}

public sealed partial class VampireRallyThrallsActionEvent : InstantActionEvent
{
    /// <summary>
    ///     Range in tiles to find thralls
    /// </summary>
    [DataField]
    public float Range = 7f;
}

public sealed partial class VampireBloodBondActionEvent : InstantActionEvent
{
    /// <summary>
    ///     Range in tiles for blood bond link
    /// </summary>
    [DataField]
    public float Range = 3f;

    /// <summary>
    ///     Blood cost per tick while active
    /// </summary>
    [DataField]
    public int BloodCostPerTick = 5;

    /// <summary>
    ///     Tick interval
    /// </summary>
    [DataField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(2);
}

public sealed partial class VampireMassHysteriaActionEvent : InstantActionEvent
{
    /// <summary>
    ///     Range in tiles to affect targets
    /// </summary>
    [DataField]
    public float Range = 8f;

    /// <summary>
    ///     Duration of the flash effect in seconds
    /// </summary>
    [DataField]
    public TimeSpan FlashDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     Duration of the hysteria vision effect in seconds
    /// </summary>
    [DataField]
    public TimeSpan HysteriaDuration = TimeSpan.FromSeconds(30);
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_Starlight/Effects/vampire/sound_hallucinations_im_here1.ogg");
}

#endregion

#region Gargantua

public sealed partial class VampireBloodSwellActionEvent : InstantActionEvent
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(30);
    /// <summary>
    ///     Total blood required for the enhanced unarmed damage bonus.
    /// </summary>
    [DataField]
    public float EnhancedThreshold = 400f;

    /// <summary>
    ///     Bonus blunt damage added to unarmed hits when enhanced.
    /// </summary>
    [DataField]
    public float MeleeBonusDamage = 14f;
}

public sealed partial class VampireBloodRushActionEvent : InstantActionEvent
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Movement speed multiplier while Blood Rush is active.
    /// </summary>
    [DataField]
    public float SpeedMultiplier = 1.5f;
}

public sealed partial class VampireSeismicStompActionEvent : InstantActionEvent
{
    /// <summary>
    ///     Radius of the stomp effect in tiles
    /// </summary>
    [DataField]
    public float Radius = 3f;

    /// <summary>
    ///     Distance to throw targets in tiles
    /// </summary>
    [DataField]
    public float ThrowDistance = 3f;
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/Footsteps/largethud.ogg");
}

public sealed partial class VampireOverwhelmingForceActionEvent : InstantActionEvent;

public sealed partial class VampireDemonicGraspActionEvent : WorldTargetActionEvent
{
    /// <summary>
    ///     Maximum range of the grasp projectile
    /// </summary>
    [DataField]
    public float Range = 15f;

    /// <summary>
    ///     Duration of immobilization in seconds
    /// </summary>
    [DataField]
    public TimeSpan ImmobilizeDuration = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Speed of the grasp projectile
    /// </summary>
    [DataField]
    public float ProjectileSpeed = 15f;
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_Starlight/Effects/vampire/exit_blood.ogg");
}

public sealed partial class VampireChargeActionEvent : WorldTargetActionEvent
{
    /// <summary>
    ///     Brute damage dealt to creatures on impact
    /// </summary>
    [DataField]
    public float CreatureDamage = 60f;

    /// <summary>
    ///     Distance to throw creatures on impact
    /// </summary>
    [DataField]
    public float CreatureThrowDistance = 5f;

    /// <summary>
    ///     Structural damage dealt to structures/machinery
    /// </summary>
    [DataField]
    public float StructuralDamage = 150f;

    /// <summary>
    ///     Charge movement speed
    /// </summary>
    [DataField]
    public float ChargeSpeed = 35f;
    
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/Footsteps/largethud.ogg");
}

/// <summary>
/// Event for syncing demonic grasp projectile visuals
/// </summary>
[Serializable, NetSerializable]
public sealed class VampireDemonicGraspVisualEvent : EntityEventArgs
{
    public NetEntity Source { get; }
    public NetCoordinates Target { get; }
    public float Speed { get; }

    public VampireDemonicGraspVisualEvent(NetEntity source, NetCoordinates target, float speed)
    {
        Source = source;
        Target = target;
        Speed = speed;
    }
}

#endregion
