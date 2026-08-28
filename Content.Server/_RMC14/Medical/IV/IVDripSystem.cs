using System.Diagnostics.CodeAnalysis;
using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Medical.IV;
using Content.Shared.Body.Components;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Medical.IV;

public sealed partial class IVDripSystem : SharedIVDripSystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private  SharedSolutionContainerSystem _solutionContainer = default!;

    private bool TryGetBloodstream(
        EntityUid attachedTo,
        [NotNullWhen(true)] out Entity<SolutionComponent>? solEnt,
        [NotNullWhen(true)] out Solution? solution,
        out Entity<SolutionComponent>? bloodstreamSolution)
    {
        solEnt = default;
        solution = default;
        bloodstreamSolution = default;
        if (!TryComp(attachedTo, out BloodstreamComponent? attachedStream) ||
            !_solutionContainer.TryGetSolution(attachedTo, attachedStream.BloodSolutionName, out solEnt, out solution))
        {
            return false;
        }

        bloodstreamSolution = attachedStream.BloodSolution;
        return true;
    }

    protected override void DoRip(DamageSpecifier? damage, EntityUid attached, EntityUid? user, ProtoId<EmotePrototype> ripEmote, bool predict)
    {
        base.DoRip(damage, attached, user, ripEmote, predict);
        _chat.TryEmoteWithoutChat(attached, ripEmote);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var ivs = EntityQueryEnumerator<IVDripComponent>();
        while (ivs.MoveNext(out var ivId, out var ivComp))
        {
            if (ivComp.AttachedTo is not { } attachedTo)
                continue;

            if (!InRange(ivId, attachedTo, ivComp.Range))
                DetachIV((ivId, ivComp), null, true, false);

            if (time < ivComp.TransferAt)
                continue;

            if (_itemSlots.GetItemOrNull(ivId, ivComp.Slot) is not { } pack)
                continue;

            if (!TryComp(pack, out BloodPackComponent? packComponent))
                continue;

            ivComp.TransferAt = time + ivComp.TransferDelay;

            if (!_solutionContainer.TryGetSolution(pack, packComponent.Solution, out var packSolEnt, out var packSol))
                continue;

            if (!TryGetBloodstream(attachedTo, out var streamSolEnt, out var streamSol, out var attachedStream))
                continue;

            if (ivComp.Injecting)
            {
                if (TryComp<BloodstreamComponent>(attachedTo, out var bsComp))
                {
                    // 1. Remove the full amount from the pack first
                    var taken = _solutionContainer.SplitSolution(packSolEnt.Value, ivComp.TransferAmount);

                    // 2. Separate Chems from Blood based on whitelist
                    // 'chems' gets the non-matching reagents. 'taken' keeps the Blood.
                    var chems = taken.SplitSolutionWithout(taken.Volume, packComponent.TransferableReagents);

                    // 3. Inject Blood -> Blood Stream
                    if (taken.Volume > 0)
                    {
                        // Check if it fits
                        if (streamSol.AvailableVolume >= taken.Volume)
                        {
                            _solutionContainer.TryAddSolution(streamSolEnt.Value, taken);
                        }
                        else
                        {
                            // If full, put blood back in pack
                            _solutionContainer.TryAddSolution(packSolEnt.Value, taken);
                        }
                    }

                    // 4. Inject Chems -> Chem Stream
                    if (chems.Volume > 0)
                    {
                        if (_solutionContainer.TryGetInjectableSolution(attachedTo, out var chemSolEnt, out var chemSol) &&
                            chemSol.AvailableVolume >= chems.Volume)
                        {
                            _solutionContainer.TryAddSolution(chemSolEnt.Value, chems);
                        }
                        else
                        {
                            // If full or no chem stream, put drugs back in pack
                            _solutionContainer.TryAddSolution(packSolEnt.Value, chems);
                        }
                    }

                    Dirty(packSolEnt.Value);
                }
            }
            else
            {
                if (packSol.Volume < packSol.MaxVolume)
                {
                    _solutionContainer.TryTransferSolution(packSolEnt.Value, streamSol, ivComp.TransferAmount);
                    Dirty(streamSolEnt.Value);
                }
            }

            Dirty(ivId, ivComp);
            UpdateIVVisuals((ivId, ivComp));
            UpdatePackVisuals((pack, packComponent));
        }

        var packs = EntityQueryEnumerator<BloodPackComponent>();
        while (packs.MoveNext(out var packId, out var packComp))
        {
            if (packComp.AttachedTo is not { } attachedTo)
                continue;

            if (!InRange(packId, attachedTo, packComp.Range))
                DetachPack((packId, packComp), null, true, false);

            if (time < packComp.TransferAt)
                continue;

            packComp.TransferAt = time + packComp.TransferDelay;

            if (!_solutionContainer.TryGetSolution(packId, packComp.Solution, out var packSolEnt, out var packSol))
                continue;

            if (!TryGetBloodstream(attachedTo, out var streamSolEnt, out var streamSol, out var attachedStream))
                continue;

            if (packComp.Injecting)
            {
                if (TryComp<BloodstreamComponent>(attachedTo, out var bsComp))
                {
                    // 1. Remove the full amount from the pack first
                    var taken = _solutionContainer.SplitSolution(packSolEnt.Value, packComp.TransferAmount);

                    // 2. Separate Chems (Drugs) from Blood based on whitelist
                    var chems = taken.SplitSolutionWithout(taken.Volume, packComp.TransferableReagents);

                    // 3. Inject Blood -> Blood Stream
                    if (taken.Volume > 0)
                    {
                        if (streamSol.AvailableVolume >= taken.Volume)
                        {
                            _solutionContainer.TryAddSolution(streamSolEnt.Value, taken);
                        }
                        else
                        {
                            _solutionContainer.TryAddSolution(packSolEnt.Value, taken);
                        }
                    }

                    // 4. Inject Chems -> Chem Stream
                    if (chems.Volume > 0)
                    {
                        if (_solutionContainer.TryGetInjectableSolution(attachedTo, out var chemSolEnt, out var chemSol) &&
                            chemSol.AvailableVolume >= chems.Volume)
                        {
                            _solutionContainer.TryAddSolution(chemSolEnt.Value, chems);
                        }
                        else
                        {
                            _solutionContainer.TryAddSolution(packSolEnt.Value, chems);
                        }
                    }

                    Dirty(packSolEnt.Value);
                }
            }
            else
            {
                if (packSol.Volume < packSol.MaxVolume)
                {
                    _solutionContainer.TryTransferSolution(packSolEnt.Value, streamSol, packComp.TransferAmount);
                    Dirty(streamSolEnt.Value);
                }
            }

            Dirty(packId, packComp);
            UpdatePackVisuals((packId, packComp));
        }
    }
}
