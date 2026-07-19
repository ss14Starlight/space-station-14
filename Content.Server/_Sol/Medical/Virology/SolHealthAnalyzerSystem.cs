using System.Globalization;
using System.Linq;
using Content.Server._Sol.Medical.Allergy;
using Content.Shared._Sol.Medical.Allergy;
using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared._Sol.Medical.Virology.Events;
using Content.Shared._Starlight.Medical.Body.Prototypes;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Fills organ status and debug virology lines into health analyzer UI state.
/// </summary>
public sealed class SolHealthAnalyzerSystem : EntitySystem
{
    private static readonly string[] VitalOrganSlots =
    [
        "brain", "eyes", "heart", "lungs", "liver", "kidneys", "stomach", "tongue",
    ];

    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly PathogenSystem _pathogen = default!;
    [Dependency] private readonly SurgeryInfectionSystem _surgeryInfection = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly AllergySystem _allergies = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HealthAnalyzerVirologyFillEvent>(OnFill);
    }

    private void OnFill(ref HealthAnalyzerVirologyFillEvent args)
    {
        args.State.Organs = BuildOrganStatus(args.Target);
        args.State.Allergies = _allergies.GetAllergyDisplayNames(args.Target).ToList();

        if (!args.Debug)
            return;

        args.State.DebugLines = BuildDebugLines(args.Target);
    }

    public List<(NetEntity OrganEntity, string OrganName, string Status)> BuildOrganStatus(EntityUid target)
    {
        var result = new List<(NetEntity, string, string)>();
        if (!TryComp<BodyComponent>(target, out var body))
            return result;

        var presentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (organUid, _) in _body.GetBodyOrgans(target, body))
        {
            var rawName = MetaData(organUid).EntityName;
            var name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(rawName);
            var status = "Healthy";
            var protoId = MetaData(organUid).EntityPrototype?.ID ?? string.Empty;

            foreach (var slot in VitalOrganSlots)
            {
                if (protoId.Contains(slot, StringComparison.OrdinalIgnoreCase) ||
                    rawName.Contains(slot, StringComparison.OrdinalIgnoreCase))
                {
                    presentKeys.Add(slot);
                }
            }

            if (TryComp<DamageableComponent>(organUid, out var damageable))
            {
                var total = damageable.TotalDamage.Float();
                status = total switch
                {
                    <= 0 => "Healthy",
                    < 20 => "Damaged",
                    < 50 => "Failing",
                    _ => "Critical",
                };
            }

            result.Add((GetNetEntity(organUid), name, status));
        }

        foreach (var slot in GetExpectedVitalSlots(body))
        {
            if (presentKeys.Contains(slot))
                continue;

            var label = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(slot);
            result.Add((NetEntity.Invalid, label, "Missing"));
        }

        return result;
    }

    private HashSet<string> GetExpectedVitalSlots(BodyComponent body)
    {
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (body.Prototype != null &&
            _prototypes.TryIndex(body.Prototype.Value, out BodyPrototype? proto) &&
            proto != null)
        {
            foreach (var slot in proto.Slots.Values)
            {
                foreach (var organSlot in slot.Organs.Keys)
                {
                    if (VitalOrganSlots.Contains(organSlot))
                        expected.Add(organSlot);
                }
            }
        }
        else
        {
            foreach (var slot in VitalOrganSlots)
                expected.Add(slot);
        }

        return expected;
    }

    public List<string> BuildDebugLines(EntityUid target)
    {
        var lines = new List<string>
        {
            $"VirologyStation={_pathogen.IsVirologyEnabledAt(target)}",
        };

        if (TryComp<PathogenCarrierComponent>(target, out var carrier))
        {
            foreach (var infection in carrier.Infections)
            {
                var traits = "n/a";
                if (_pathogen.TryResolvePathogen(infection.PathogenId, out var def) && def != null)
                {
                    traits = def.TraitIds.Count == 0 ? "none" : string.Join(',', def.TraitIds);
                    lines.Add(
                        $"Infection {infection.PathogenId} ({def.DisplayName}): stage={infection.Stage} dose={infection.Dose:F2} chassis={def.ChassisId} traits={traits} runtime={def.IsRuntimeStrain}");
                }
                else
                {
                    lines.Add(
                        $"Infection {infection.PathogenId}: stage={infection.Stage} dose={infection.Dose:F2} started={infection.StageStartedAt} surgery={infection.FromSurgery}");
                }
            }
        }
        else
        {
            lines.Add("Infections: none");
        }

        if (TryComp<ImmunityComponent>(target, out var immunity))
        {
            foreach (var entry in immunity.Entries)
                lines.Add($"Immunity {entry.Identity}: strength={entry.Strength:F2} expires={entry.ExpiresAt}");
        }

        if (TryComp<SurfaceContaminationComponent>(target, out var surface))
        {
            lines.Add($"Dirty={surface.IsDirty}");
            foreach (var entry in surface.Contaminants)
                lines.Add($"Contaminant {entry.PathogenId}={entry.Load:F2}");
        }

        if (TryComp<AirborneContaminantComponent>(target, out var air))
        {
            foreach (var entry in air.Contaminants)
                lines.Add($"Airborne {entry.PathogenId}={entry.Load:F2}");
        }

        var ppeContact = _pathogen.GetPpeCoefficient(target, PathogenTransmission.Contact);
        var ppeAir = _pathogen.GetPpeCoefficient(target, PathogenTransmission.Airborne);
        lines.Add($"PPE contact={ppeContact:F2} airborne={ppeAir:F2}");

        var mods = _surgeryInfection.CalculateModifiers(target, target, new List<EntityUid>(), failed: false);
        lines.Add(
            $"SurgeryInfection final={mods.FinalChance:F3} base={mods.BaseChance:F3} mask={mods.MaskMultiplier:F2} tool={mods.ToolMultiplier:F2} glove={mods.GloveMultiplier:F2} env={mods.EnvironmentMultiplier:F2}");

        if (TryComp<AllergyComponent>(target, out var allergy))
        {
            foreach (var id in allergy.Allergies)
                lines.Add($"Allergy={id}");
        }

        return lines;
    }
}
