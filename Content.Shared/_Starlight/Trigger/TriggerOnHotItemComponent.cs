using Content.Shared.Trigger.Components.Triggers;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Trigger;

/// <summary>
/// Triggers when an item used on this entity reports IsHotEvent.IsHot as true.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TriggerOnHotItemComponent : BaseTriggerOnXComponent;
