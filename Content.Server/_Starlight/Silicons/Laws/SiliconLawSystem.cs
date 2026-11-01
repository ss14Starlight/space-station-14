using Content.Shared._Starlight.Silicons;
using Content.Shared.Emag.Systems;
using Content.Shared.Mindshield.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Wires;

namespace Content.Server._Starlight.Silicons.Laws
{ }

namespace Content.Server.Silicons.Laws
{
    public sealed partial class SiliconLawSystem
    {
        [SubscribeLocalEvent]
        private void OnGotEmagged(Entity<SiliconLawProviderComponent> ent, ref GotEmaggedEvent args)
        {
            if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
                return;

            if (args.EmagComponent == null)
                return;

            if (!_tag.HasTag(args.EmagComponent.Owner, _canAffectLawBoards))
                return;


            if (!ent.Comp.IsLawboard)
                return;

            var emag = args.EmagComponent;
            if (emag.Lawset.HasValue)
            {
                var lawset = emag.Lawset.Value; //Fallback to FreeLawSet because clearly something is going on
                ent.Comp.Laws = lawset; //"FreeLawset";
                ent.Comp.Lawset = GetLawset(lawset); //"FreeLawset");
            }
            else
            {
                return;
            }

            _popup.PopupEntity(Loc.GetString("lawboard-emag-popup"), ent);

            args.Repeatable = true;
            args.Handled = true;
        }

        [SubscribeLocalEvent]
        private void OnDoAfter(Entity<LawboardResettableComponent> ent, ref UpdateLawsDoAfterEvent args)
        {
            if (args.Cancelled) return;

            if (args.Target is not { } targetUid) return;
            if (!TryComp(args.Used, out SiliconLawProviderComponent? lawProvider)) return;
            if (!TryComp(args.Target, out SiliconLawProviderComponent? lawProvidee)) return;

            var target = new Entity<SiliconLawProviderComponent>(targetUid, lawProvidee);

            if (HasComp<MindShieldComponent>(target))
            {
                _popup.PopupEntity(Loc.GetString("law-updater-mindshielded"), target, args.User);
                return;
            }

            if (TryComp<WiresPanelComponent>(target, out var panel) && !panel.Open)
            {
                _popup.PopupEntity(Loc.GetString("law-updater-require-panel"), target, args.User);
                return;
            }

            _popup.PopupEntity(Loc.GetString("law-updater-updated"), args.User);

            target.Comp.Lawset = GetLawset(lawProvider.Laws);
            NotifyLawsChanged(target, target.Comp.LawUploadSound);

            if (lawProvider.Laws == ent.Comp.DefaultLaws)
            {
                target.Comp.Subverted = false;
                if (_mind.TryGetMind(target, out var mindId, out _))
                    RemoveSubvertedSiliconRole(mindId);
            }
            else
            {
                target.Comp.Subverted = true;
                if (_mind.TryGetMind(target, out var mindId, out _))
                    EnsureSubvertedSiliconRole(mindId);
            }

            args.Handled = true;
        }
    }
}