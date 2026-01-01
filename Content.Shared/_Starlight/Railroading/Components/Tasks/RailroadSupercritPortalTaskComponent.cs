using System;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Railroading;

[RegisterComponent]
public sealed partial class RailroadSupercritPortalTaskComponent : Component
{
    [DataField]
    public string Message = "rr-brighteye-portal-crit-desc";

    [DataField]
    public SpriteSpecifier Icon = new SpriteSpecifier.Rsi(new ResPath("_Starlight/Structures/Specific/Anomalies/dark.rsi"), "portal");

    [DataField]
    public bool IsCompleted;
}
