using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

// ReSharper disable CheckNamespace
namespace Content.Shared.Actions.Components;

public sealed partial class ActionComponent
{
    [DataField, AutoNetworkedField]
    public bool PreservePositionOnRemoval;
}
