using System.Diagnostics.CodeAnalysis;
using Content.Shared.Access.Components;
using Content.Shared.PDA;

namespace Content.Shared.Access.Systems;

public sealed partial class IdExaminableSystem : EntitySystem
{
    /// <summary>
    /// Tries to get an ID from the specified slot on the specified entity.
    /// Check for both standalone ID Cards and PDAs containing ID Cards.
    /// </summary>
    private bool TryGetIdFromSlot(EntityUid uid, string slot, [NotNullWhen(true)] out IdCardComponent? idComp)
    {
        if (_inventorySystem.TryGetSlotEntity(uid, slot, out var idUid))
        {
            // PDA
            if (TryComp(idUid, out PdaComponent? pda) &&
                TryComp<IdCardComponent>(pda.ContainedId, out var id))
            {
                idComp = id;
                return true;
            }
            // ID Card
            if (TryComp(idUid, out id))
            {
                idComp = id;
                return true;
            }
        }

        idComp = null;
        return false;
    }
}
