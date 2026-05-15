// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.UI;

internal sealed class RecentPalette : GridContainer
{
    private readonly StarlightColorPicker _owner;
    private readonly Action _onChanged;

    public RecentPalette(StarlightColorPicker owner, int columns)
    {
        _owner = owner;
        Columns = columns;
        HSeparationOverride = 2;
        VSeparationOverride = 2;
        _onChanged = Refresh;

        Refresh();
    }

    public void Refresh()
    {
        RemoveAllChildren();

        var slots = _owner.Recents.Slots;
        for (var i = 0; i < slots.Count; i++)
        {
            var color = slots[i];
            var allowed = color is not { } c || _owner.IsColorAllowed(c);
            var isActive = i == _owner.ActiveSlot;
            AddChild(new SwatchButton(_owner, i, color, allowed, isActive));
        }
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _owner.Recents.Changed += _onChanged;
        Refresh();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _owner.Recents.Changed -= _onChanged;
    }
}
