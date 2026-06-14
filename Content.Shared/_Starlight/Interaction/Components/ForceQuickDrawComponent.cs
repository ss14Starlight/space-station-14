using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Interaction.Components;

/// <summary>
/// This comp is only used as a marker to disable attempts to remove items from internal containers with smart equip
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ForceQuickDrawComponent : Component;
