// ReSharper disable CheckNamespace

using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Jittering;
using Content.Server.Mind;
using Content.Server.Stunnable;
using Content.Shared.Actions;
using Content.Shared.Anomaly;
using Content.Shared.Anomaly.Components;
using Content.Shared.Anomaly.Effects;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Gibbing;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

using Content.Shared.NPC.Systems;
using Content.Shared.NPC.Prototypes;

namespace Content.Server.Anomaly.Effects;

// Far Horizons - made partial
public sealed partial class InnerBodyAnomalySystem : SharedInnerBodyAnomalySystem
{
    [Dependency] private NpcFactionSystem _npcFaction = default!; // Starlight

    private static readonly ProtoId<NpcFactionPrototype> _cosmicCultFaction = "CosmicCult"; // Starlight

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

        ProcessComponents(ent, injectedAnom.Components, true);

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

        // Starlight Start
        ent.Comp.Injected = false;

        if (ent.Comp.AddedCosmicCultFaction)
        {
            _npcFaction.RemoveFaction(ent.Owner, _cosmicCultFaction);
            ent.Comp.AddedCosmicCultFaction = false;
        }

        Dirty(ent);
        // Starlight End
        if (_proto.Resolve(ent.Comp.InjectionProto, out var injectedAnom))
            ProcessComponents(ent, injectedAnom.Components, false); // Starlight

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

        // ent.Comp.Injected = false; // Starlight Edit: Moved
        // RemCompDeferred<AnomalyComponent>(ent); // Starlight Edit: Removed
    }

    private void ProcessComponents(
        EntityUid target,
        ComponentRegistry components,
        bool add)
    {
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
                else
                {
                    EntityManager.AddComponent(target, comp.Value);
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
                EntityManager.RemoveComponent(target, componentType);
        }
    }
}
