using Robust.Shared.Prototypes;
using Content.Server.Actions;
using Content.Shared._Starlight.Deathmatch;
using Content.Shared.Hands.EntitySystems;

namespace Content.Server._Starlight.Deathmatch;

public sealed partial class DeathmatchSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeathmatchComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, DeathmatchComponent comp, ref ComponentStartup args)
    {
        // add actions
        foreach (var actionId in comp.BaseDeathmatchActions)
            _actions.AddAction(uid, actionId);
    }

}
