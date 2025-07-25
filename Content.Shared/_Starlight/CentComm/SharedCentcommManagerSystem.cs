namespace Content.Shared._Starlight.CentComm;
using Robust.Shared.Serialization;

public abstract partial class SharedCentCommManagmentConsoleSystem : EntitySystem {

    public override void Initialize()
    {
        base.Initialize();

    }

}

[Serializable, NetSerializable]
public enum ManagementConsoleUIKey
{
    Key
}
