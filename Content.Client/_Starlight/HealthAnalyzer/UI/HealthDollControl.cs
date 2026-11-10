// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared._Starlight.Medical.HealthAnalyzer;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.HealthAnalyzer.UI;

public sealed class HealthDollControl : Control
{
    private static readonly ResPath _rsi = new("/Textures/_Starlight/Interface/Medical/health_doll.rsi");

    private static readonly string[] _drawOrder =
        ["chest", "groin", "head", "l_leg", "r_leg", "l_foot", "r_foot", "l_arm", "r_arm", "l_hand", "r_hand"];

    public HealthDollControl() => SetSize = new Vector2(64, 64);

    public void Update(List<HealthAnalyzerDollPart>? parts)
    {
        RemoveAllChildren();
        Visible = parts is { Count: > 0 };
        if (parts == null)
            return;

        var levels = new Dictionary<string, byte>();
        foreach (var part in parts)
            levels[part.Part] = part.Level;

        foreach (var name in _drawOrder)
        {
            if (!levels.TryGetValue(name, out var level))
                continue;

            var missing = level == HealthAnalyzerDollPart.Missing;
            var rect = new AnimatedTextureRect();
            rect.SetFromSpriteSpecifier(new SpriteSpecifier.Rsi(_rsi, $"{name}{(missing ? 0 : Math.Min(level, (byte) 5))}"));
            rect.DisplayRect.TextureScale = new Vector2(2, 2);
            if (missing)
                rect.Modulate = Color.Black;
            AddChild(rect);
        }
    }
}
