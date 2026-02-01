using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Antags.Vampires.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShadowSnareComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Blunt", 20f }
        }
    };

    [DataField, AutoNetworkedField]
    public float BlindDuration = 20f;

    /// <summary>
    /// Radius for extinguishing nearby lights
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LightExtinguishRadius = 5f;

    /// <summary>
    /// Walk speed modifier for the ensnare effect
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WalkSpeed = 0.4f;

    /// <summary>
    /// Sprint speed modifier for the ensnare effect
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SprintSpeed = 0.4f;

    /// <summary>
    /// Time for someone else to free the ensnared target
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FreeTime = 3f;

    /// <summary>
    /// Time for the target to free themselves
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BreakoutTime = 8f;

    [DataField]
    public SoundSpecifier TriggerSound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");

    [DataField]
    public EntProtoId EnsnarePrototype = "VampireShadowSnareEnsnare";
}
