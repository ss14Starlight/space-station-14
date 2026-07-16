using Content.Client._Starlight.AlertAwareArmor.UI;
using Content.Shared._Starlight.AlertAwareArmor;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.AlertAwareArmor;

/// <summary>
/// A helper methods to show alert level specific resistances.
/// </summary>
public sealed class AlertAwareArmorSystem : SharedAlertAwareArmorSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlertAwareArmorComponent, GetVerbsEvent<ExamineVerb>>(OnArmorVerbExamine);
    }

    private void OnArmorVerbExamine(EntityUid uid, AlertAwareArmorComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !component.ShowArmorOnExamine)
            return;

        args.Verbs.Add(new ExamineVerb
        {
            Text = Loc.GetString("alert-aware-armor-examinable-verb-text"),
            Message = Loc.GetString("alert-aware-armor-examinable-verb-message"),
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Act = () => new AlertAwareArmorWindow(component).OpenCentered(),
            ClientExclusive = true,
            CloseMenu = true,
        });
    }
}
