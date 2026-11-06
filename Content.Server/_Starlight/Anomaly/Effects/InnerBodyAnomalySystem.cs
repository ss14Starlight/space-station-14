// ReSharper disable CheckNamespace

using Content.Shared.Actions;
using Content.Shared.Anomaly.Components;
using Content.Shared.Anomaly.Effects;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

using Content.Shared.NPC.Systems;
using Content.Shared.NPC.Prototypes;

namespace Content.Server.Anomaly.Effects;

// Far Horizons - made partial
public sealed partial class InnerBodyAnomalySystem : SharedInnerBodyAnomalySystem
{
    [Dependency] private NpcFactionSystem _npcFaction = default!;

    private static readonly ProtoId<NpcFactionPrototype> _cosmicCultFaction = "CosmicCult";

    public bool AddedCosmicCultFaction;

    private void AddAnomalyToBody(Entity<InnerBodyAnomalyComponent> ent)
    {
        if (!_proto.Resolve(ent.Comp.InjectionProto, out var injectedAnom))
            return;

        if (ent.Comp.Injected)
            return;

        ent.Comp.Injected = true;

        if (ent.Comp.InjectionProto == "CosmicAnomalyInjection")
        {
            if (!_npcFaction.IsMember(ent.Owner, _cosmicCultFaction))
            {
                _npcFaction.AddFaction(ent.Owner, _cosmicCultFaction);
                ent.Comp.AddedCosmicCultFaction = true;
            }
        }

        ProcessComponents(ent,ent, injectedAnom.Components, true);

        _stun.TryUpdateParalyzeDuration(ent, TimeSpan.FromSeconds(ent.Comp.StunDuration));
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(ent.Comp.StunDuration), true);

        if (ent.Comp.StartSound is not null)
            _audio.PlayPvs(ent.Comp.StartSound, ent);

        if (ent.Comp.StartMessage is not null &&
            _mind.TryGetMind(ent, out _, out var mindComponent) &&
            _player.TryGetSessionById(mindComponent.UserId, out var session))
        {
            var message = Loc.GetString(ent.Comp.StartMessage);
            var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
            _chat.ChatMessageToOne(ChatChannel.Server,
                message,
                wrappedMessage,
                default,
                false,
                session.Channel,
                _messageColor);

            _popup.PopupEntity(message, ent, ent, PopupType.MediumCaution);

            _adminLog.Add(LogType.Anomaly,LogImpact.Medium,$"{ToPrettyString(ent)} became anomaly host.");
        }
        Dirty(ent);
    }

    private void OnAnomalySupercritical(Entity<InnerBodyAnomalyComponent> ent, ref AnomalySupercriticalEvent args)
    {
        if (!TryComp<BodyComponent>(ent, out var body))
            return;

        _gibbing.Gib(ent.Owner);
    }

    private void RemoveAnomalyFromBody(Entity<InnerBodyAnomalyComponent> ent)
    {
        if (!ent.Comp.Injected)
            return;

        ent.Comp.Injected = false;

        if (ent.Comp.AddedCosmicCultFaction)
        {
            _npcFaction.RemoveFaction(ent.Owner, _cosmicCultFaction);
            ent.Comp.AddedCosmicCultFaction = false;
        }

        Dirty(ent);

        ProcessComponents(ent, ent, ent.Comp.AddedComps, false);

        _stun.TryUpdateParalyzeDuration(ent, TimeSpan.FromSeconds(ent.Comp.StunDuration));

        if (ent.Comp.EndMessage is not null &&
            _mind.TryGetMind(ent, out _, out var mindComponent) &&
            _player.TryGetSessionById(mindComponent.UserId, out var session))
        {
            var message = Loc.GetString(ent.Comp.EndMessage);
            var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
            _chat.ChatMessageToOne(ChatChannel.Server,
                message,
                wrappedMessage,
                default,
                false,
                session.Channel,
                _messageColor);

            _popup.PopupEntity(message, ent, ent, PopupType.MediumCaution);

            _adminLog.Add(LogType.Anomaly, LogImpact.Medium,$"{ToPrettyString(ent)} is no longer a host for the anomaly.");
        }
    }

    private void ProcessComponents(
        EntityUid target,
        InnerBodyAnomalyComponent anomComp,
        ComponentRegistry components,
        bool add)
    {
        if (add) anomComp.AddedComps.Clear();

        foreach (var comp in components)
        {
            var componentType = comp.Value.Component.GetType();
            if (add)
            {
                if (comp.Value.Component is ActionGrantComponent actionGrantComp &&
                    TryComp<ActionGrantComponent>(target, out var oldComp))
                {
                    _actionGrant.AddActions((target, oldComp), actionGrantComp.Actions);
                }
                else if(!HasComp(target, componentType))
                {
                    EntityManager.AddComponent(target, comp.Value);
                    anomComp.AddedComps.Add(comp.Key,  comp.Value);
                }

                continue;
            }

            if (comp.Value.Component is ActionGrantComponent removeActionGrantComp &&
                TryComp<ActionGrantComponent>(target, out var removeOldComp))
            {
                _actionGrant.RemoveActions((target, removeOldComp), removeActionGrantComp.Actions);
                continue;
            }

            if (HasComp(target, componentType))
                RemComp(target, componentType);
        }
    }
}
