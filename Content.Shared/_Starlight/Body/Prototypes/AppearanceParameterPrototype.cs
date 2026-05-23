// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Prototypes;

/// <summary>
/// Abstract base for any tunable appearance parameter exposed by body part layers
/// </summary>
public abstract partial class AppearanceParameterPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;
}
