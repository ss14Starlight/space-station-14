using Content.Shared.Explosion;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Input.Binding;
using Content.Shared._Starlight.Medical.Body.Part; // Starlight

namespace Content.Server.Hands.Systems
{
    public sealed partial class HandsSystem : SharedHandsSystem
    {
        // Starlight: Moved dependencies to shared since nothing in here needs them anymore.

        public override void Initialize()
        {
            base.Initialize();

            // Starlight: Moved disarmed event listener to shared for prediction.

            // Starlight Start: Reverted NuBody
            SubscribeLocalEvent<HandsComponent, BodyPartAddedEvent>(HandleBodyPartAdded);
            SubscribeLocalEvent<HandsComponent, BodyPartRemovedEvent>(HandleBodyPartRemoved);
            // Starlight End: Reverted NuBody

            SubscribeLocalEvent<HandsComponent, ComponentGetState>(GetComponentState);

            SubscribeLocalEvent<HandsComponent, BeforeExplodeEvent>(OnExploded);

            // Starlight begin: Moved DropHandItemsEvent, physics query assignment, and input command binding to shared for prediction.
        }

        public override void Shutdown()
        {
            base.Shutdown();

            CommandBinds.Unregister<HandsSystem>();
        }

        private void GetComponentState(EntityUid uid, HandsComponent hands, ref ComponentGetState args)
        {
            args.State = new HandsComponentState(hands);
        }


        private void OnExploded(Entity<HandsComponent> ent, ref BeforeExplodeEvent args)
        {
            if (ent.Comp.DisableExplosionRecursion)
                return;

            foreach (var held in EnumerateHeld(ent.AsNullable()))
            {
                args.Contents.Add(held);
            }
        }

        // Starlight: Moved OnDisarmed to shared for prediction.
        // Starlight Start: Reverted NuBody
        private void HandleBodyPartAdded(Entity<HandsComponent> ent, ref BodyPartAddedEvent args)
        {
            if (args.Part.Comp.PartType != BodyPartType.Hand)
                return;

            // If this annoys you, which it should.
            // Ping Smugleaf.
            var location = args.Part.Comp.Symmetry switch
            {
                BodyPartSymmetry.None => HandLocation.Middle,
                BodyPartSymmetry.Left => HandLocation.Left,
                BodyPartSymmetry.Right => HandLocation.Right,
                _ => throw new ArgumentOutOfRangeException(nameof(args.Part.Comp.Symmetry))
            };

            AddHand(ent.AsNullable(), args.Slot, location);
        }

        private void HandleBodyPartRemoved(EntityUid uid, HandsComponent component, ref BodyPartRemovedEvent args)
        {
            if (args.Part.Comp.PartType != BodyPartType.Hand)
                return;

            RemoveHand(uid, args.Slot);
        }
        // Starlight End: Reverted NuBody
        #region interactions

        // Starlight: Moved HandleThrowItem, OnDropHandItems, and ThrowHeldItem to shared for prediction.

        #endregion
    }
}
