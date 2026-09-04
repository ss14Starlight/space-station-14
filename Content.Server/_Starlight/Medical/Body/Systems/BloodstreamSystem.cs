using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Forensics;

namespace Content.Server._Starlight.Medical.Body.Systems;

public sealed class BloodstreamSystem : SharedBloodstreamSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodstreamComponent, GenerateDnaEvent>(OnDnaGenerated);
    }

    // forensics is not predicted yet
    private void OnDnaGenerated(Entity<BloodstreamComponent> entity, ref GenerateDnaEvent args)
    {
        if (!RefreshBloodData(entity))
            Log.Error("Unable to set bloodstream DNA, solution entity could not be resolved");
    }
}
