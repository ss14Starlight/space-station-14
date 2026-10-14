using Robust.Shared.GameStates;

using Content.Shared.Trigger.Components.Triggers;

namespace Content.Shared._Starlight.Trigger.Components;

/// <summary>
///     Add this component to an entity to trigger whenever it gets pointed at.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TriggerOnPointedAtComponent : BaseTriggerOnXComponent;
