using System.Linq;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Storage;
using Content.Shared._Starlight.Storage.Components;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Starlight.Storage;

public sealed partial class PickLastSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PickLastComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
    }

    private void OnGetAlternativeVerbs(EntityUid uid, PickLastComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<StorageComponent>(uid, out var storage))
            return;

        var user = args.User;

        var enabled = storage.Container.ContainedEntities.Any(item => _whitelistSystem.IsWhitelistPassOrNull(comp.Whitelist, item));

        // alt-click / alt-z to pick an item
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => TryPick(uid, comp, storage, user),
            Impact = LogImpact.Low,
            Text = Loc.GetString(comp.VerbText),
            Disabled = !enabled,
            Message = enabled ? null : Loc.GetString(comp.EmptyText, ("storage", uid))
        });
    }

    private void TryPick(EntityUid uid, PickLastComponent comp, StorageComponent storage, EntityUid user)
    {
        var entities = storage.Container.ContainedEntities.Where(item => _whitelistSystem.IsWhitelistPassOrNull(comp.Whitelist, item)).ToArray();

        if (entities.Length == 0)
            return;

        var picked = entities[^1];

        // if it fails to go into a hand of the user, will be on the storage
        _container.AttachParentToContainerOrGrid((picked, Transform(picked)));

        _hands.TryPickupAnyHand(user, picked);

        if (storage.StorageRemoveSound is not null)
        {
            _audio.PlayPredicted(storage.StorageRemoveSound, uid, user, storage.StorageRemoveSound.Params);
        }
    }
}
