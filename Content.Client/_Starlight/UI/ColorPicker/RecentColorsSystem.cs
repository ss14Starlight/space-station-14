// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client._Starlight.UI;

/// <summary>
/// Client-side singleton holding the recent-color palette shared by every
/// <see cref="StarlightColorPicker"/> in the session.
/// </summary>
public sealed class RecentColorsSystem : EntitySystem
{
    public const int Capacity = 32;

    private readonly Color?[] _slots = new Color?[Capacity];

    public IReadOnlyList<Color?> Slots => _slots;

    public event Action? Changed;

    /// <summary>
    /// Atomically claims the first empty slot for a new picker by writing a placeholder white
    /// into it. Returns the slot index, or -1 if every slot is occupied.
    /// </summary>
    public int Reserve()
    {
        for (var i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] is null)
            {
                _slots[i] = Color.White;
                Changed?.Invoke();
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Overwrites the color stored in <paramref name="slot"/>. No-op if the slot index is out
    /// of range (e.g. picker has no active slot).
    /// </summary>
    public void SetSlot(int slot, Color color)
    {
        if (slot < 0 || slot >= _slots.Length)
            return;
        if (_slots[slot] is { } existing && existing == color)
            return;
        _slots[slot] = color;
        Changed?.Invoke();
    }
}
