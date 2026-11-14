using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.StationRadio.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class RecordCrateComponent : Component
{
    [DataField]
    public string ContainerId = "records";

    /// <summary>
    /// How many records fit, one sprite spine each.
    /// </summary>
    [DataField]
    public int Capacity = 8;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public SoundSpecifier? InsertSound = new SoundPathSpecifier("/Audio/_Starlight/RadioStation/Vinyls/vinylInsert.ogg");

    [DataField]
    public SoundSpecifier? RemoveSound = new SoundPathSpecifier("/Audio/_Starlight/RadioStation/Vinyls/vinylEject.ogg");
}
