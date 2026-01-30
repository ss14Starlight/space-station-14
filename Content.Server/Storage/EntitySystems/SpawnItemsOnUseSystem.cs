using Content.Server.Administration.Logs;
using Content.Server.Cargo.Systems;
using Content.Server.Storage.Components;
using Content.Shared.Cargo;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using static Content.Shared.Storage.EntitySpawnCollection;

namespace Content.Server.Storage.EntitySystems
{
    public sealed class SpawnItemsOnUseSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly PricingSystem _pricing = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SpawnItemsOnUseComponent, UseInHandEvent>(OnUseInHand);
            SubscribeLocalEvent<SpawnItemsOnUseComponent, GetVerbsEvent<AlternativeVerb>>(OnGetWrapperVerbs); // 🌟Starlight🌟
            SubscribeLocalEvent<SpawnItemsOnUseComponent, PriceCalculationEvent>(CalculatePrice, before: new[] { typeof(PricingSystem) });
        }

        private void CalculatePrice(EntityUid uid, SpawnItemsOnUseComponent component, ref PriceCalculationEvent args)
        {
            var ungrouped = CollectOrGroups(component.Items, out var orGroups);

            foreach (var entry in ungrouped)
            {
                var protUid = Spawn(entry.PrototypeId, MapCoordinates.Nullspace);

                // Calculate the average price of the possible spawned items
                args.Price += _pricing.GetPrice(protUid) * entry.SpawnProbability * entry.GetAmount(getAverage: true);

                Del(protUid);
            }

            foreach (var group in orGroups)
            {
                foreach (var entry in group.Entries)
                {
                    var protUid = Spawn(entry.PrototypeId, MapCoordinates.Nullspace);

                    // Calculate the average price of the possible spawned items
                    args.Price += _pricing.GetPrice(protUid) *
                                  (entry.SpawnProbability / group.CumulativeProbability) *
                                  entry.GetAmount(getAverage: true);

                    Del(protUid);
                }
            }

            args.Handled = true;
        }

        private void OnUseInHand(EntityUid uid, SpawnItemsOnUseComponent component, UseInHandEvent args)
        {
            if (args.Handled)
                return;

        // 🌟Starlight🌟 start
        //Functionality was moved and slightly modified
            var coords = Transform(args.User).Coordinates;
            SpawnItems(uid, component, args.User, coords);
            args.Handled = true;
        }

        private void SpawnItems(EntityUid uid, SpawnItemsOnUseComponent component, EntityUid user, EntityCoordinates coordinates)
        {
            // If starting with zero or less uses, this component is a no-op
            if (component.Uses <= 0)
                return;

            var spawnEntities = GetSpawns(component.Items, _random);
            EntityUid? entityToPlaceInHands = null;

            foreach (var proto in spawnEntities)
            {
                entityToPlaceInHands = Spawn(proto, coordinates);
                _adminLogger.Add(LogType.EntitySpawn, LogImpact.Low, $"{ToPrettyString(user)} used {ToPrettyString(uid)} which spawned {ToPrettyString(entityToPlaceInHands.Value)}");
            }

            // The entity is often deleted, so play the sound at its position rather than parenting
            if (component.Sound != null)
                _audio.PlayPvs(component.Sound, coordinates);

            component.Uses--;

            // Delete entity only if component was successfully used
            if (component.Uses <= 0)
            {
                // Don't delete the entity in the event bus, so we queue it for deletion.
                // We need the free hand for the new item, so we send it to nullspace.
                _transform.DetachEntity(uid, Transform(uid));
                QueueDel(uid);
            }

            if (entityToPlaceInHands != null)
                _hands.PickupOrDrop(user, entityToPlaceInHands.Value);

        }

        private void OnGetWrapperVerbs(EntityUid uid, SpawnItemsOnUseComponent component, ref GetVerbsEvent<AlternativeVerb> args)
        {
            if (!args.CanAccess || !args.CanInteract || (component.RequireHands && args.Hands != null) )
                return;

            if (_hands.IsHolding(args.User, uid))
                return;

            var user = args.User;
            var coords = Transform(uid).Coordinates;

            args.Verbs.Add(new AlternativeVerb()
            {
                Act = () => SpawnItems(uid, component, user, coords),
                Text = Loc.GetString("delivery-open-verb"),
            });
        }
        // 🌟Starlight🌟 end
    }
}
