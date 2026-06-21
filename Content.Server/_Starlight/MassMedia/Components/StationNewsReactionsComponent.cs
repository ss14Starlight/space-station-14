namespace Content.Server._Starlight.MassMedia.Components;

[RegisterComponent]
public sealed partial class StationNewsReactionsComponent : Component
{
    public readonly Dictionary<int, HashSet<NetEntity>> ReactedByArticle = new();
    public readonly Dictionary<int, HashSet<NetEntity>> ViewedByArticle = new();
}
