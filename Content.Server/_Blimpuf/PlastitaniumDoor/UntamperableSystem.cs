using Content.Server.Doors.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Interaction;
using Content.Shared.Tools.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Blimpuf.PlastitaniumDoor
{
    public sealed partial class UntamperableSystem : EntitySystem
    {
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<UntamperableComponent, GotEmaggedEvent>(OnEmagged);
            SubscribeLocalEvent<UntamperableComponent, InteractUsingEvent>(OnInteractUsing, before: new[] { typeof(DoorSystem) });
            SubscribeLocalEvent<UntamperableComponent, AccessReaderConfigurationAttemptEvent>(OnConfigAttempt);
        }

        private void OnEmagged(EntityUid uid, UntamperableComponent component, ref GotEmaggedEvent args)
        {
            args.Handled = true;
        }

        private void OnInteractUsing(EntityUid uid, UntamperableComponent component, InteractUsingEvent args)
        {
            if (HasComp<ToolComponent>(args.Used))
            {
                _audio.PlayPvs(component.DenyChangeSound, uid);
                args.Handled = true;
            }
        }
        private void OnConfigAttempt(EntityUid uid, UntamperableComponent component, AccessReaderConfigurationAttemptEvent args)
        {
            if (component.AccessChangeDisabled)
            {
                _audio.PlayPvs(component.DenyChangeSound, uid);
                args.Cancel();
            }
        }
    }
}
