// ReSharper disable CheckNamespace
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Medical;

/// <summary>Marks the wearable defibrillator and its touch-triggered shocks.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMODefibGlovesComponent : Component
{
    [DataField]
    public SoundSpecifier TouchShockSound = new SoundCollectionSpecifier("sparks");

    [DataField]
    public TimeSpan ReadySoundLead = TimeSpan.FromMilliseconds(527);

    [DataField]
    public TimeSpan KnockdownDuration = TimeSpan.FromSeconds(2);
}

[Serializable, NetSerializable]
public sealed partial class CMODefibGlovesDoAfterEvent : SimpleDoAfterEvent;
