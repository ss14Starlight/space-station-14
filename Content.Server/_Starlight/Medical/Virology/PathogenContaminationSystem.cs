using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.GameTicking;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Owns the round's station contamination snapshot.
/// </summary>
public sealed partial class PathogenContaminationSystem : EntitySystem
{
    private readonly PathogenContaminationPool _contamination = new();

    public float Contamination => _contamination.Total;

    public float GetContamination(PathogenType type)
        => _contamination.Get(type);

    public IReadOnlyList<PathogenType> GetDominantTypes()
        => _contamination.GetDominantTypes();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public void SetContamination(IReadOnlyDictionary<PathogenType, float> contributions)
        => _contamination.Set(contributions);

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
        => _contamination.Reset();
}
