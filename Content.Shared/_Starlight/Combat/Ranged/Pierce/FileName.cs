using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.Starlight.Antags.Abductor;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Combat.Ranged.Pierce;
[RegisterComponent, NetworkedComponent]
public sealed partial class PierceableComponent : Component
{
    [DataField("level")]
    public PierceLevel Level = PierceLevel.Metal;
}
[Serializable, NetSerializable]
public enum PierceLevel : byte
{
    Flesh,
    Wood,
    Metal,
    HardenedMetal,
    Rock,
}