using Content.Shared.Item;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._TP.Kitchen.Components;

/// <summary>
///     Lets the owner entity 'deepfry' items.
///     Created by Cookie (FatherCheese) for Trieste Port 14.
/// </summary>
[RegisterComponent]
[ComponentProtoName("DeepFryer")]
public sealed partial class SharedDeepFryerComponent : Component
{
    [DataField]
    public bool IsEnabled;

    [ViewVariables]
    public bool IsBroken;

    [DataField]
    public float CookTimePerLevel = 15.0F;

    [DataField]
    public ProtoId<ItemSizePrototype> MaxItemSize = "Huge";

    [DataField]
    public SoundPathSpecifier FryingSound = new("/Audio/_TP/Machines/Kitchen/frying_idle.ogg");

    [DataField]
    public SoundPathSpecifier Buzzer = new("/Audio/_TP/Machines/Kitchen/frying_buzzer.ogg");

    public readonly string ContainerId = "fryer_slots";

    public readonly string SolutionContainerId = "fryer";
}

[Serializable, NetSerializable]
public enum DeepFryerVisuals : byte
{
    Base,
    Active,
}
