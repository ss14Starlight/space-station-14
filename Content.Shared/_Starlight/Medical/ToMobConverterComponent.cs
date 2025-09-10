namespace Content.Shared._Starlight.Medical;

[RegisterComponent]
public sealed partial class ToMobConverterComponent : Component
{
    public HashSet<EntityUid> Converting = new();
}