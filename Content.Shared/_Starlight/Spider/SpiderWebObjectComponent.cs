// ReSharper disable once CheckNamespace
namespace Content.Shared.Spider;

public sealed partial class SpiderWebObjectComponent
{
    /// <summary>
    /// Whether a spider spawning webs can replace this. Can be set to false for web structures.
    /// </summary>
    [DataField] public bool Replaceable = true;
}
