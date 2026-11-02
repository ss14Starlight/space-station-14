// ReSharper disable CheckNamespace
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Medical;

/// <summary>Wearable defibrillator with contact shocks.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMODefibGlovesComponent : Component
{
    /// <summary>Contact shock sound.</summary>
    [DataField]
    public SoundSpecifier TouchShockSound = new SoundCollectionSpecifier("sparks");

    /// <summary>Time from the ready sound to do-after completion.</summary>
    [DataField]
    public TimeSpan ReadySoundLead = TimeSpan.FromMilliseconds(527);

    /// <summary>Living-target stun and knockdown duration.</summary>
    [DataField]
    public TimeSpan KnockdownDuration = TimeSpan.FromSeconds(2);
}

/// <summary>Dead-target defibrillation do-after.</summary>
[Serializable, NetSerializable]
public sealed partial class CMODefibGlovesDoAfterEvent : SimpleDoAfterEvent;
