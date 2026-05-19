using Content.Server.Doors.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.Interaction;
using Content.Shared.Tools.Components;

namespace Content.Server._Blimpuf.PlastitaniumDoor
{
    public sealed class UntamperableSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<UntamperableComponent, GotEmaggedEvent>(OnEmagged);
            SubscribeLocalEvent<UntamperableComponent, InteractUsingEvent>(OnInteractUsing, before: new[] { typeof(DoorSystem) });
        }

        private void OnEmagged(EntityUid uid, UntamperableComponent component, ref GotEmaggedEvent args)
        {
            args.Handled = true;
        }

        private void OnInteractUsing(EntityUid uid, UntamperableComponent component, InteractUsingEvent args)
        {
            if (HasComp<ToolComponent>(args.Used))
            {
                args.Handled = true;
            }
        }
    }
}
