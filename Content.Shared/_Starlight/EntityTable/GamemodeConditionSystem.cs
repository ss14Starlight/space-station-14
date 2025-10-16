using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.EntityTable;

public sealed partial class GamemodeConditionSystem : EntitySystem
{
    
    [Dependency] private readonly INetManager _netMan = default!;
    public override void Initialize()
    {
        base.Initialize();
    }

    public bool CheckGamemode(HashSet<string> conditions)
    {
        var check = new PresetConditionCheckEvent(conditions);
        if (_netMan.IsServer)
        {
            RaiseLocalEvent(check);
        }
        else
        {
            RaiseNetworkEvent(check);
        }

        return check.Valid;
    }
}


[Serializable, NetSerializable]
public sealed class PresetConditionCheckEvent(HashSet<string> presets) : EntityEventArgs
{
    public HashSet<string> Presets = presets;
    public bool Valid = false;
}