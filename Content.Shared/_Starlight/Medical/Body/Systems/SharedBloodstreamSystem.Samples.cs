using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;

namespace Content.Shared._Starlight.Medical.Body.Systems;

public abstract partial class SharedBloodstreamSystem
{
    /// <summary>
    /// Create an external sample of the donor's native blood without draining its bloodstream.
    /// </summary>
    public Solution CreateBloodSample(Entity<BloodstreamComponent?> donor, FixedPoint2 amount)
    {
        if (!Resolve(donor, ref donor.Comp, logMissing: false) || amount <= 0)
            return new Solution();

        var sample = donor.Comp.BloodReferenceSolution.Clone();
        sample.ScaleTo(amount);
        SolutionContainer.PrepareExternalSample(donor.Owner, sample);
        return sample;
    }
}
