using Robust.Shared.GameStates;

namespace Content.Shared._Blimpuf.Contraband;

/// <summary>
/// Allows an equipped entity to show contraband details on examine.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShowContrabandDetailsComponent : Component;
