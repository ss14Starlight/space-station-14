using Content.Shared._Starlight.Devil;
using Content.Server.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Server.RandomMetadata;
using Robust.Server.Audio;
using Robust.Shared.Prototypes;
using Content.Shared.Speech.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Vampire.Components;
using System.Linq;

namespace Content.Server._Starlight.Devil;

public sealed partial class DevilSystem : SharedDevilSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly RandomMetadataSystem _randomMetadata = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevilComponent, ComponentStartup>(OnStartup, before: [typeof(DamageableSystem)]);

        SubscribeLocalEvent<DevilComponent, SummonDemonicContractEvent>(OnSummonDemonicContract);

        SubscribeContract();
        SubscribeDamned();
        SubscribeBanish();
    }

    private void OnStartup(EntityUid uid, DevilComponent devilComp, ref ComponentStartup args)
    {
        foreach (var action in devilComp.BaseActions) _actions.AddAction(uid, action);

        devilComp.TrueName = _randomMetadata.GetRandomFromSegments(devilComp.NameSegments, devilComp.NameFormat);

        EnsureComp<ActiveListenerComponent>(uid); // for banish listen events
        EnsureComp<UnholyComponent>(uid);
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

    /// <summary>
    /// Get the number of souls the devil has damned, with optional damnation(s) that have to be met
    /// </summary>
    public int GetSoulsDamned(EntityUid devil, List<ProtoId<DamnationPrototype>>? requiredDamnations)
    {
        int damned = 0;
        var query = AllEntityQuery<DamnedComponent>();
        while (query.MoveNext(out var uid, out var damnedComp))
        {
            if(damnedComp.DamnedBy != devil) continue; // not us

            if(requiredDamnations == null) damned++;
            else
            {
                if(!damnedComp.Damnations.Except(requiredDamnations).Any()) damned++;
            }
        }

        return damned;
    }
    #endregion
}