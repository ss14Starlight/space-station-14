using Content.Server.Power.EntitySystems;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Radio.Components;

namespace Content.Server._Starlight.Radio.EntitySystems {}

namespace Content.Server.Radio.EntitySystems {
    public sealed partial class RadioDeviceSystem {
        [SubscribeLocalEvent]
        private void OnItemToggled(EntityUid uid, RadioMicrophoneComponent comp, ref ItemToggledEvent args)
            => SetMicrophoneEnabled(uid, null,
                args.Activated && !(comp.PowerRequired && !this.IsPowered(uid, EntityManager)));

        [SubscribeLocalEvent]
        private void OnItemToggled(EntityUid uid, RadioSpeakerComponent comp, ref ItemToggledEvent args)
            => SetSpeakerEnabled(uid, null, args.Activated);
    }
}