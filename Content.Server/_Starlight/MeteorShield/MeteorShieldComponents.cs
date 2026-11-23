namespace Content.Server._Starlight.MeteorShield;

/// <summary>
/// Lists the satellites around the station, switches them and shows how much of the station they cover.
/// </summary>
[RegisterComponent, Access(typeof(MeteorShieldSystem))]
public sealed partial class MeteorShieldConsoleComponent : Component;
