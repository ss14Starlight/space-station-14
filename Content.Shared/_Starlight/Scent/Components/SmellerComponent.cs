using Content.Shared._Starlight.Scent.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Starlight.Scent.Components;

/// <summary>
/// Grants an entity a toggleable scent-vision (Toggle Smelling) and the ability to lock onto a
/// single scent once identified via Smell Target. Give to whatever has a sensitive nose.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), Access(typeof(SharedScentSystem))]
public sealed partial class SmellerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Sniffing;

    // If set, scent-vision only shows markers matching this ScentId.
    [DataField, AutoNetworkedField]
    public string? TrackedScentId;

    [DataField("toggleAction", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ToggleAction = "ActionToggleSniff";

    [DataField]
    public EntityUid? ToggleActionEntity;

    // Granted alongside TrackedScentId. Only exists while there's something to sneeze away.
    [DataField("sneezeAction", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string SneezeAction = "ActionSneeze";

    [DataField]
    public EntityUid? SneezeActionEntity;

    // Granted alongside Sniffing. Only usable while scent-vision is on.
    [DataField("sniffObjectAction", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string SniffObjectAction = "ActionSniffObject";

    [DataField]
    public EntityUid? SniffObjectActionEntity;

    // Which object's results window is open, if any. Used by OnSmellerMove to close it.
    [DataField]
    public EntityUid? SniffTarget;

    [DataField]
    public float SniffRange = 2f;

    // How long Toggle Smelling locks out after walking into smoke, regardless of contents.
    [DataField]
    public TimeSpan SmokeLockout = TimeSpan.FromSeconds(10);

    [DataField]
    public SoundSpecifier SneezeSound =
        new SoundPathSpecifier("/Audio/_Starlight/Scent/dog_sneeze.ogg", AudioParams.Default.WithVolume(-4));

    [DataField]
    public SoundSpecifier SniffSound = new SoundPathSpecifier("/Audio/_Starlight/Scent/dog_sniff.ogg");

    [DataField]
    public TimeSpan SniffDelay = TimeSpan.FromSeconds(1.5);

    // Partial perceivers see markers for less of their life, even less for container-sourced
    // markers, and lose all perception while breathing internals.
    [DataField, AutoNetworkedField]
    public ScentPerception Perception = ScentPerception.Full;
}

public enum ScentPerception : byte
{
    Full,
    Partial,
}
