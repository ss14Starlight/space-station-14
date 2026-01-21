using System.Linq;
using Content.Server._Starlight.Mentor;
using Content.Server.Access.Components;
using Content.Server.Access.Systems;
using Content.Server.Popups;
using Content.Shared.Access.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;

namespace Content.Server._Starlight.Access.Systems
{
    public sealed class NCTDataChipSystem : EntitySystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<NCTDataChipComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<NCTDataChipComponent, UseInHandEvent>(OnUseInHand);
            SubscribeLocalEvent<NCTDataChipComponent, AfterInteractEvent>(OnAfterInteract);
        }

        private void OnExamined(Entity<NCTDataChipComponent> ent, ref ExaminedEvent args)
        {
            if (ent.Comp.Trainee != "")
                args.PushMarkup(Loc.GetString("nctdatachip-trainee", ("targetName", ent.Comp.Trainee)));

            args.PushMarkup(Loc.GetString("nctdatachip-notice"));
            args.PushMarkup(Loc.GetString("nctdatachip-notice2"));
        }

        private void OnUseInHand(EntityUid uid, NCTDataChipComponent component, UseInHandEvent args)
        {
            if (args.Handled)
                return;

            if (!HasComp<NCTAgentComponent>(args.User))
            {
                _popupSystem.PopupEntity(Loc.GetString("nctdatachip-denied"), uid, args.User);
                args.Handled = true;
                return;
            }

            if (!TryComp<AccessComponent>(uid, out var access))
                return;

            component.Trainee = "";

            access.Tags.Clear();

            _popupSystem.PopupEntity(Loc.GetString("nctdatachip-reset"), uid, args.User);
            Dirty(uid, access);

            args.Handled = true;
        }

        private void OnAfterInteract(EntityUid uid, NCTDataChipComponent component, AfterInteractEvent args)
        {
            if (args.Target == null || !args.CanReach ||
                !TryComp<AccessComponent>(args.Target, out var targetAccess) || !TryComp<IdCardComponent>(args.Target, out var trainee))
                return;

            if (!HasComp<NCTAgentComponent>(args.User))
            {
                _popupSystem.PopupEntity(Loc.GetString("nctdatachip-denied"), uid, args.User);
                return;
            }

            if (!TryComp<AccessComponent>(uid, out var access))
                return;

            if (trainee.FullName is not null)
                component.Trainee = trainee.FullName;

            access.Tags.Clear();
            access.Tags.UnionWith(targetAccess.Tags.Except(component.BlacklistTags));

            _popupSystem.PopupEntity(Loc.GetString("nctdatachip-scanned", ("targetName", component.Trainee)), args.Target.Value, args.User);
            Dirty(uid, access);
        }
    }
}
