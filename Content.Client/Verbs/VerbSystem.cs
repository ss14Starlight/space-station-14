using System.Diagnostics.CodeAnalysis;
using Content.Client.Examine;
using Content.Client.Popups;
using Content.Shared.CCVar;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Verbs
{
    [UsedImplicitly]
    public sealed partial class VerbSystem : SharedVerbSystem
    {
        [Dependency] private PopupSystem _popupSystem = default!;
        [Dependency] private ExamineSystem _examine = default!;
        [Dependency] private SpriteTreeSystem _tree = default!;
        [Dependency] private TagSystem _tagSystem = default!;
        [Dependency] private IStateManager _stateManager = default!;
        [Dependency] private IEyeManager _eyeManager = default!;
        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private SharedContainerSystem _containers = default!;
        [Dependency] private IConfigurationManager _cfg = default!;
        [Dependency] private EntityLookupSystem _lookup = default!;

        private float _lookupSize;

        private static readonly ProtoId<TagPrototype> HideContextMenuTag = "HideContextMenu";

        /// <summary>
        ///     These flags determine what entities the user can see on the context menu.
        /// </summary>
        public MenuVisibility Visibility;

        public Action<VerbsResponseEvent>? OnVerbsResponse;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeNetworkEvent<VerbsResponseEvent>(HandleVerbResponse);
            Subs.CVar(_cfg, CCVars.GameEntityMenuLookup, OnLookupChanged, true);
        }

        private void OnLookupChanged(float val)
        {
            _lookupSize = val;
        }

        /// <summary>
        /// Get all of the entities in an area for displaying on the context menu.
        /// </summary>
        /// <returns>True if any entities were found.</returns>
        public bool TryGetEntityMenuEntities(MapCoordinates targetPos, [NotNullWhen(true)] out List<EntityUid>? entities)
            => TryGetEntityMenuEntities(targetPos, null, null, out entities); // Starlight

        /// <summary>
        ///     Ask the server to send back a list of server-side verbs, and for now return an incomplete list of verbs
        ///     (only those defined locally).
        /// </summary>
        public SortedSet<Verb> GetVerbs(NetEntity target, EntityUid user, List<Type> verbTypes, out List<VerbCategory> extraCategories, bool force = false)
        {
            if (!target.IsClientSide())
                RaiseNetworkEvent(new RequestServerVerbsEvent(target, verbTypes, adminRequest: force));

            // Some admin menu interactions will try get verbs for entities that have not yet been sent to the player.
            if (!TryGetEntity(target, out var local))
            {
                extraCategories = new();
                return new();
            }

            return GetLocalVerbs(local.Value, user, verbTypes, out extraCategories, force);
        }


        /// <summary>
        ///     Execute actions associated with the given verb.
        /// </summary>
        /// <remarks>
        ///     Unless this is a client-exclusive verb, this will also tell the server to run the same verb.
        /// </remarks>
        public void ExecuteVerb(EntityUid target, Verb verb)
        {
            ExecuteVerb(GetNetEntity(target), verb);
        }

        /// <summary>
        ///     Execute actions associated with the given verb.
        /// </summary>
        /// <remarks>
        ///     Unless this is a client-exclusive verb, this will also tell the server to run the same verb.
        /// </remarks>
        public void ExecuteVerb(NetEntity target, Verb verb)
        {
            if ((_remoteControl.ControlledEntity ?? _playerManager.LocalEntity) is not { } user) // Starlight
                return;

            // is this verb actually valid?
            if (verb.Disabled)
            {
                // maybe send an informative pop-up message.
                if (!string.IsNullOrWhiteSpace(verb.Message))
                    _popupSystem.PopupEntity(FormattedMessage.RemoveMarkupOrThrow(verb.Message), user);

                return;
            }

            if (verb.ClientExclusive || target.IsClientSide())
                // is this a client exclusive (gui) verb?
                ExecuteVerb(verb, user, GetEntity(target));
            else
                RaisePredictiveEvent(new ExecuteVerbEvent(target, verb));
        }

        private void HandleVerbResponse(VerbsResponseEvent msg)
        {
            OnVerbsResponse?.Invoke(msg);
        }
    }
}
