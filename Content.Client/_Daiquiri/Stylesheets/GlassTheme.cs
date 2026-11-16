using System;
using System.Collections.Generic;
using System.Linq;
using Content.Client.Stylesheets;
using Content.Shared._Starlight.CCVar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;

namespace Content.Client._Daiquiri.Stylesheets;

/// <summary>
/// </summary>
public static class GlassTheme
{
    public sealed record LiquidGlassParams(
        bool Enabled,
        float BlurPixels,
        float RefractionPixels,
        float Highlight,
        float Opacity,
        float TintStrength,
        Color? TintColor,
        float CornerRadius,
        int Quality,
        bool Animate);

    public static LiquidGlassParams ReadLiquidParams(IConfigurationManager cfg)
    {
        var enabled = cfg.GetCVar(StarlightCCVars.UILiquidGlassEnabled);
        var blur = Math.Clamp(cfg.GetCVar(StarlightCCVars.UILiquidGlassBlur), 0, 100) / 100f * 18f;
        var refraction = Math.Clamp(cfg.GetCVar(StarlightCCVars.UILiquidGlassRefraction), 0, 100) / 100f * 10f;
        var highlight = Math.Clamp(cfg.GetCVar(StarlightCCVars.UILiquidGlassHighlight), 0, 100) / 100f;
        var opacity = Math.Clamp(cfg.GetCVar(StarlightCCVars.UILiquidGlassOpacity), 0, 100) / 100f;
        var tint = Math.Clamp(cfg.GetCVar(StarlightCCVars.UILiquidGlassTint), 0, 100) / 100f;

        Color? tintColor = null;
        if (cfg.GetCVar(StarlightCCVars.UILiquidGlassTintEnabled)
            && Color.TryFromHex(cfg.GetCVar(StarlightCCVars.UILiquidGlassTintColor), out var customTint))
        {
            tintColor = customTint;
        }

        var corner = Math.Clamp(cfg.GetCVar(StarlightCCVars.UILiquidGlassCornerRadius), 0, 100) / 100f * 24f;
        var quality = Math.Clamp(cfg.GetCVar(StarlightCCVars.UILiquidGlassQuality), 0, 2);
        var animate = cfg.GetCVar(StarlightCCVars.UILiquidGlassAnimate);

        return new LiquidGlassParams(enabled, blur, refraction, highlight, opacity, tint, tintColor, corner, quality, animate);
    }

    public static Stylesheet MakeGlass(Stylesheet source, LiquidGlassParams pars, out int glassifiedBoxes)
    {
        var count = 0;
        var rules = new List<StyleRule>();
        foreach (var rule in source.Rules)
            rules.Add(CloneRule(rule, pars, ref count));
        glassifiedBoxes = count;
        return new Stylesheet(rules.ToArray());
    }

    private static StyleRule CloneRule(
        StyleRule rule,
        LiquidGlassParams pars,
        ref int count)
    {
        if (!pars.Enabled)
            return rule;

        var isButton = rule.Selector is SelectorElement element
            && element.ElementType is { } elementType
            && (typeof(BaseButton).IsAssignableFrom(elementType)
                || typeof(ContainerButton).IsAssignableFrom(elementType))
            && !IsSegmentedButton(element);

        var props = new List<StyleProperty>();
        foreach (var prop in rule.Properties)
            props.Add(CloneProperty(prop, pars, ref count, isButton));
        // Textured panels (window backgrounds, chat panels, ...) have no alpha of
        // their own, so make them translucent via modulate-self instead.
        // Existing modulate-self tints (e.g. PDA) get their alpha scaled, not skipped.
        var panelIdx = props.FindIndex(p => p.Name == PanelContainer.StylePropertyPanel
            && p.Value is StyleBoxTexture);
        if (panelIdx >= 0)
        {
            var modIdx = props.FindIndex(p => p.Name == Control.StylePropertyModulateSelf
                && p.Value is Color c && c.A >= 0.99f);
            if (modIdx >= 0)
            {
                var tint = (Color) props[modIdx].Value;
                props[modIdx] = new StyleProperty(Control.StylePropertyModulateSelf,
                    new Color(tint.R, tint.G, tint.B, pars.Opacity));
                count++;
            }
            else if (!props.Any(p => p.Name == Control.StylePropertyModulateSelf))
            {
                props.Add(new StyleProperty(Control.StylePropertyModulateSelf,
                    new Color(1f, 1f, 1f, pars.Opacity)));
                count++;
            }
        }
        return new StyleRule(rule.Selector, props);
    }

    /// <summary>
    /// </summary>
    private static bool IsSegmentedButton(SelectorElement element)
    {
        if (element.ElementClasses is not { } classes)
            return false;

        foreach (var styleClass in classes)
        {
            if (styleClass == StyleClass.ButtonOpenLeft
                || styleClass == StyleClass.ButtonOpenRight
                || styleClass == StyleClass.ButtonOpenBoth)
            {
                return true;
            }
        }

        return false;
    }

    private static StyleProperty CloneProperty(
        StyleProperty property,
        LiquidGlassParams pars,
        ref int count,
        bool isButton)
    {
        if (property.Value is not StyleBoxFlat box)
            return property;

        if (box.BackgroundColor.A < 0.99f)
            return property;

        count++;
        return new StyleProperty(property.Name, new LiquidGlassStyleBox(box, pars, isButton));
    }
}