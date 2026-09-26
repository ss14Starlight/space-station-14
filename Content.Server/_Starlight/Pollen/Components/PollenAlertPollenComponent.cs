namespace Content.Server._Starlight.Pollen.Components;

/// <summary>
/// Purchased from the pollen shop. Purely a marker - while present, this
/// entity releases a distress pollen alert to nearby Dionas the instant it
/// drops from Alive straight into Critical. No other mob-state transition
/// triggers it (crit->dead, dead->crit, etc. are ignored).
/// </summary>
[RegisterComponent]
public sealed partial class PollenAlertPollenComponent : Component
{
}
