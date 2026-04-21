// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Robust.Shared.Collections;

namespace Content.Shared._Starlight.Body.Components;

[RegisterComponent]
public sealed partial class SLBodyComponent : Component
{

    //Cached list of all body parts for quick enumeration
    [NonSerialized] public ValueList<Entity<SLBodyPartComponent>> BodyParts = new();
}
