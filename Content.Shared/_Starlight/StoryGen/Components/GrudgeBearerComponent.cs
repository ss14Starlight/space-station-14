
namespace Content.Shared._Starlight.StoryGen;

/// <summary>
/// The owner of a Book of Grudges
/// </summary>
[Access(typeof(SharedGrudgeSystem))]
public sealed partial class GrudgeBearerComponent : Component
{
    public EntityUid? Book;

    [DataField("judginess")]
    public float judginess = 1.0f;
}
