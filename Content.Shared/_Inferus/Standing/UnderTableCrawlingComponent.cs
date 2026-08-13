using Robust.Shared.GameStates;

namespace Content.Shared._Inferus.Standing;

/// <summary>
/// Allows the entity to toggle crawling under furniture with a keybind while downed
/// Visual + speed only; does not change physics collision (see Starlight CrawlUnder for that)
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UnderTableCrawlingComponent : Component
{
    [DataField, AutoNetworkedField]
    public float CrawlingUnderSpeedModifier = 0.5f;

    /// <summary>
    /// If true, the entity is choosing to crawl under furniture
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsCrawlingUnder = false;

    [DataField, AutoNetworkedField]
    public int NormalDrawDepth = (int) DrawDepth.DrawDepth.Mobs;

    [DataField, AutoNetworkedField]
    public int CrawlingUnderDrawDepth = (int) DrawDepth.DrawDepth.SmallMobs;
}
