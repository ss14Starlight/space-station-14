using Content.Server.Research.Systems;
using Content.Shared.Research.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Seeds research points on tutorial R&amp;D servers after practice entities spawn.
/// Clients on the same grid auto-register via <see cref="ResearchSystem"/> MapInit.
/// </summary>
public sealed partial class TutorialResearchBootstrapSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> ResearchServerTag = "TutorialResearchServer";

    private const int SeedPoints = 10000;

    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private TagSystem _tags = default!;

    public void TryConfigureOnGrid(EntityUid gridUid)
    {
        var query = EntityQueryEnumerator<ResearchServerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var server, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            if (!_tags.HasTag(uid, ResearchServerTag))
                continue;

            if (server.Points >= SeedPoints)
                continue;

            _research.ModifyServerPoints(uid, SeedPoints - server.Points, server);
        }
    }
}
