using Content.Shared._Starlight.Devil;
using Content.Server.Actions;
using Content.Shared.Hands.EntitySystems;
using Robust.Server.Audio;

namespace Content.Server._Starlight.Devil;

public sealed partial class DevilSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevilComponent, ComponentStartup>(OnStartup);

        // actions (may be seperated into own file later)
        SubscribeLocalEvent<DevilComponent, SummonDemonicContractEvent>(OnSummonDemonicContract);

        SubscribeContract();
    }

    private void OnStartup(EntityUid uid, DevilComponent devilComp, ref ComponentStartup args)
    {
        foreach (var action in devilComp.BaseActions) _actions.AddAction(uid, action);
    }

    #region abilities
    private void OnSummonDemonicContract(EntityUid uid, DevilComponent devilComp, ref SummonDemonicContractEvent args)
    {
        var paper = CreateContract(uid, devilComp);
        _hands.TryPickupAnyHand(uid, paper);

        args.Handled = true;
    }
    #endregion

    #region utility
    #endregion
}