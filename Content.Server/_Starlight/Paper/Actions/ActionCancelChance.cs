using Robust.Shared.Random;

namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionCancelChance : OnSignAction
{
    /// <summary>
    /// what is the chance of this list working. with 1 being always, and 0 being never.
    /// </summary>
    [DataField]
    public float Chance = 1.0f;

    private IRobustRandom _random = default!;

    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        return _random.NextFloat() > Chance;
    }
    
    public override void ResolveIoC()
    {
        _random = IoCManager.Resolve<IRobustRandom>();
    }
}