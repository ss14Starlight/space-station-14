using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Content.Shared.Zombies
{
    public sealed partial class ZombieComponent
    {
        [DataField]
        public float BaseZombieInfectionChance = 0.825f;
    }
}
