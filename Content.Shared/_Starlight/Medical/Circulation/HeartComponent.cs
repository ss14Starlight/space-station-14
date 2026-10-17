// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Medical.Circulation;

/// <summary>
/// A heart that pumps the blood of the body it is in. How well it pumps is its rhythm times how intact it is.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class HeartComponent : Component
{
    [DataField, AutoNetworkedField]
    public HeartRhythm Rhythm = HeartRhythm.Normal;

    /// <summary>
    /// Resting heart rate, beats per minute.
    /// </summary>
    [DataField]
    public float BaseRate = 75f;

    /// <summary>
    /// Organ damage at which the heart stops pumping at all; it pumps proportionally less on the way there.
    /// </summary>
    [DataField]
    public FixedPoint2 MaxDamage = 100;
}

[Serializable, NetSerializable]
public enum HeartRhythm : byte
{
    Normal,

    /// <summary>
    /// Irregular beating, pumps noticeably worse.
    /// </summary>
    Arrhythmia,

    /// <summary>
    /// Quivering instead of beating, barely pumps. Needs a defibrillator.
    /// </summary>
    Fibrillation,

    /// <summary>
    /// Not beating at all.
    /// </summary>
    Arrest,
}
