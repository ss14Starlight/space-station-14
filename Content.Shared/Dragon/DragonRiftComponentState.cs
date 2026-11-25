using Robust.Shared.Serialization;

namespace Content.Shared.Dragon;

[Serializable, NetSerializable]
public sealed class DragonRiftComponentState : ComponentState
{
    public DragonRiftState State;
    #region Starlight
    public int SharkMinnowLimit;
    public int Percentage;
    #endregion
}
