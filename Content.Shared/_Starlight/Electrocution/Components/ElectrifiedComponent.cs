// ReSharper disable CheckNamespace
namespace Content.Shared.Electrocution;

public sealed partial class ElectrifiedComponent
{
    [DataField, AutoNetworkedField]
    public bool HasElectrifyingWire;
}
