using Content.Shared._Starlight.Silicons;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Components;
using Content.Shared.Interaction;
using Content.Shared.Mindshield.Components;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Wires;

namespace Content.Shared._Starlight.Silicons.Laws
{ }

namespace Content.Shared.Silicons.Laws
{
    public abstract partial class SharedSiliconLawSystem
    {
        [Dependency] private SharedDoAfterSystem _doAfter = default!;

        [SubscribeLocalEvent]
        private void OnAfterInteract(Entity<SiliconLawProviderComponent> ent, ref AfterInteractEvent args)
        {
            if (HasComp<EmagComponent>(ent)) return;
            if (args.Target is not { } target) return;
            if (!HasComp<SiliconLawBoundComponent>(args.Target)) return;
            if (!HasComp<SiliconLawProviderComponent>(args.Target)) return;
            if (!TryComp(args.Target, out LawboardResettableComponent? lrComp)) return;

            // can't change own laws
            if (args.Target == args.User)
            {
                _popup.PopupEntity(Loc.GetString("law-updater-cannot-change-self"), target, args.User);
                return;
            }

            if (TryComp<WiresPanelComponent>(args.Target, out var panel) && !panel.Open)
            {
                _popup.PopupEntity(Loc.GetString("law-updater-require-panel"), target, args.User);
                return;
            }

            if (HasComp<MindShieldComponent>(args.Target))
            {
                _popup.PopupEntity(Loc.GetString("law-updater-mindshielded"), target, args.User);
                return;
            }

            _popup.PopupEntity(
                Loc.GetString("law-updater-alert", ("name", Name(args.User))),
                target, target, PopupType.Large
            );

            _doAfter.TryStartDoAfter(
                new DoAfterArgs(
                    EntityManager, args.User, lrComp.Delay,
                    new UpdateLawsDoAfterEvent(), args.Target, target: args.Target, used: args.Used
                )
                {
                    BreakOnDamage = true,
                    BreakOnMove = true,
                    BreakOnHandChange = true,
                    BreakOnDropItem = true,
                    MovementThreshold = 0.5f,
                    CancelDuplicate = true
                }
            );
        }
    }
}