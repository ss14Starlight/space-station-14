using Content.Shared._Starlight.Railroading.Events;
using Robust.Server.Player;
using Robust.Shared.Random;
using Content.Server._Starlight.Economy;
using Content.Shared._Starlight.Railroading.Components.Reward;

namespace Content.Server._Starlight.Railroading.RewardSystems;

public sealed partial class RailroadingDonationRewardSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SalarySystem _salarySystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadDonationRewardComponent, RailroadingCardCompletedEvent>(OnCompleted);
    }

    private void OnCompleted(Entity<RailroadDonationRewardComponent> ent, ref RailroadingCardCompletedEvent args)
    {
        if (!_playerManager.TryGetSessionByEntity(args.Subject, out var session))
            return;

        var credits = ent.Comp.Amount.Next(_random);
        if (credits == 0)
            return;

        _salarySystem.Donate(session, credits);
    }
}
