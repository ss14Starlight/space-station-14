using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Devil;

[Serializable, NetSerializable]
public enum DamnationsMenuUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed partial class DevilDamnationsBuiState : BoundUserInterfaceState
{
    public EntityUid Devil;

    public DevilDamnationsBuiState(EntityUid devil)
    {
        Devil = devil;
    }
}