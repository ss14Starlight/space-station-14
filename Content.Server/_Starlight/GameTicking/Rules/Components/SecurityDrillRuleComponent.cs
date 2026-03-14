using Content.Server.StationEvents.Events;

namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(SecurityDrillRule))]
public sealed partial class SecurityDrillRuleComponent : Component;