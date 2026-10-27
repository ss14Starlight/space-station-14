using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;

namespace Content.Shared._Starlight.Medical.Body.Systems;

public abstract partial class SharedBloodstreamSystem
{
    /// <summary>
    /// Create an external sample of the donor's native blood without draining its bloodstream.
    /// </summary>
    /// <param name="donor">The entity whose native blood reference and external sample policy are used.</param>
    /// <param name="amount">The volume to which the cloned native blood reference is scaled.</param>
    /// <returns>
    /// An externally prepared sample, or an empty solution if the bloodstream cannot be resolved or the amount is nonpositive.
    /// </returns>
    /// <remarks>
    /// Copies reagent metadata from the native blood reference, including any DNA stored there.
    /// It does not sample drugs or other contents of the circulating solution. Callers that also need
    /// blood loss must apply that separately; this helper is for synthesized spills such as blood vomit.
    /// </remarks>
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
