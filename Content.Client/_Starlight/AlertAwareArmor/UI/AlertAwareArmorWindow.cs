using System.Numerics;
using Content.Shared._Starlight.AlertAwareArmor;
using Content.Shared.Damage;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.AlertAwareArmor.UI;

/// <summary>
/// A window showing armor values for each alert level
/// </summary>
public sealed class AlertAwareArmorWindow : DefaultWindow
{
    public AlertAwareArmorWindow(AlertAwareArmorComponent component)
    {
        Title = Loc.GetString("alert-aware-armor-window-title");
        MinSize = new Vector2(340, 260);

        var tabs = new TabContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        Contents.AddChild(tabs);

        AddTab(tabs, Loc.GetString("alert-aware-armor-tab-default"),
            component.Modifiers, component.StaminaDamageModifier);

        foreach (var (level, data) in component.Levels)
        {
            AddTab(tabs, Loc.GetString($"alert-level-{level}"),
                data.Modifiers, data.StaminaDamageModifier ?? component.StaminaDamageModifier);
        }
    }

    private static void AddTab(TabContainer tabs, string title, DamageModifierSet modifiers, float staminaModifier)
    {
        var label = new RichTextLabel
        {
            Margin = new Thickness(8),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        label.SetMessage(BuildMessage(modifiers, staminaModifier));

        var scroll = new ScrollContainer
        {
            HScrollEnabled = false,
        };
        scroll.AddChild(label);

        tabs.AddChild(scroll);
        TabContainer.SetTabTitle(scroll, title);
    }

    /// <summary>
    /// Builds the same protection breakdown as the vanilla armor examine.
    /// </summary>
    private static FormattedMessage BuildMessage(DamageModifierSet modifiers, float staminaModifier)
    {
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("armor-examine"));

        foreach (var coefficientArmor in modifiers.Coefficients)
        {
            msg.PushNewline();

            var armorType = Loc.GetString("armor-damage-type-" + coefficientArmor.Key.ToLower());
            msg.AddMarkupOrThrow(Loc.GetString("armor-coefficient-value",
                ("type", armorType),
                ("value", MathF.Round((1f - coefficientArmor.Value) * 100, 1))
            ));
        }

        msg.PushNewline();
        var staminaType = Loc.GetString("armor-damage-type-stamina");
        msg.AddMarkupOrThrow(Loc.GetString("armor-stamina-value",
            ("type", staminaType),
            ("value", MathF.Round((1f - staminaModifier) * 100, 1))
        ));

        foreach (var flatArmor in modifiers.FlatReduction)
        {
            msg.PushNewline();

            var armorType = Loc.GetString("armor-damage-type-" + flatArmor.Key.ToLower());
            msg.AddMarkupOrThrow(Loc.GetString("armor-reduction-value",
                ("type", armorType),
                ("value", flatArmor.Value)
            ));
        }

        return msg;
    }
}
