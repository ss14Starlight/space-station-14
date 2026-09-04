
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Zombies;

[RegisterComponent]
public sealed partial class PreZombificationValuesComponent : Component
{
    public float BloodlossThreshold { get; set; } = 0.9f;
    public float MaxVolumeModifier { get; set; } = 1f;
    public FixedPoint2 BloodRefreshAmount { get; set; } = 0f;
    public List<ProtoId<NpcFactionPrototype>> OriginalFactions = [];
    public Solution BeforeZombifiedBloodReagents { get; set; } =  new();



}
