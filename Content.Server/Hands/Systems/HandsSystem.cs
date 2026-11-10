using Content.Shared.Explosion;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Input.Binding;

namespace Content.Server.Hands.Systems
{
    public sealed partial class HandsSystem : SharedHandsSystem
    {
        // Starlight: Moved dependencies to shared since nothing in here needs them anymore.

        public override void Initialize()
        {
            base.Initialize();

            // Starlight: Moved disarmed event listener to shared for prediction.

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
        // Starlight: hands come from hand body parts (BodyPartHandSystem).
        #region interactions

        // Starlight: Moved HandleThrowItem, OnDropHandItems, and ThrowHeldItem to shared for prediction.

        #endregion
    }
}
