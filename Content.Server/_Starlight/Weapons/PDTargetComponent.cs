namespace Content.Server._Starlight.Weapon;

/// <summary>
/// Mark the Entity as a Point Defense Target and will be shot down by PD Turrets, Entities shot from the same Grid as the PD Turrets wont be targetted.
/// </summary>
[RegisterComponent]
public sealed partial class PDTargetComponent : Component;