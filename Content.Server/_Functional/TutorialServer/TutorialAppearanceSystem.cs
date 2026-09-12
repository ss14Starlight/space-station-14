// TODO: REENABLE WHEN VISUALBODYSYSTEM IS IMPLEMENTED

// using Content.Shared._Functional.TutorialServer;
// using Content.Shared.Body;
// using Content.Shared.Humanoid;
// using Content.Shared.Humanoid.Markings;
// using Content.Shared.Humanoid.Prototypes;
// using Content.Shared.Preferences;
// using Robust.Shared.Prototypes;
//
// namespace Content.Server._Functional.TutorialServer;
//
// /// <summary>
// /// Gives a tutorial NPC the face it was written with: the species default, plus the hair named on
// /// <see cref="TutorialAppearanceComponent"/>.
// /// </summary>
// /// <remarks>
// /// Runs on MapInit like <c>RandomHumanoidAppearanceSystem</c>, and is the deliberate alternative to
// /// it. Markings are stored per organ rather than per layer, so the hair layer's owning organ has to
// /// be looked up from the species' marking data rather than assumed.
// /// </remarks>
// public sealed class TutorialAppearanceSystem : EntitySystem
// {
//     [Dependency] private readonly IPrototypeManager _protos = default!;
//     [Dependency] private readonly MarkingManager _markings = default!;
//     [Dependency] private readonly SharedVisualBodySystem _visualBody = default!;
//     [Dependency] private readonly HumanoidProfileSystem _humanoidProfile = default!;
//
//     public override void Initialize()
//     {
//         base.Initialize();
//
//         SubscribeLocalEvent<TutorialAppearanceComponent, MapInitEvent>(OnMapInit);
//     }
//
//     private void OnMapInit(Entity<TutorialAppearanceComponent> ent, ref MapInitEvent args)
//     {
//         if (!TryComp<HumanoidProfileComponent>(ent, out var humanoid))
//             return;
//
//         var profile = HumanoidCharacterProfile.DefaultWithSpecies(humanoid.Species, humanoid.Sex);
//
//         ApplyHair(ent.Comp, humanoid.Species, profile.Appearance);
//
//         _visualBody.ApplyProfileTo(ent.Owner, profile);
//         _humanoidProfile.ApplyProfileTo(ent.Owner, profile);
//     }
//
//     /// <summary>
//     /// Writes the configured hair into the appearance, on whichever organ owns each hair layer for
//     /// this species. A species with no hair layer at all simply gets nothing.
//     /// </summary>
//     private void ApplyHair(
//         TutorialAppearanceComponent comp,
//         ProtoId<SpeciesPrototype> species,
//         HumanoidCharacterAppearance appearance)
//     {
//         foreach (var (organ, data) in _markings.GetMarkingData(species))
//         {
//             foreach (var layer in data.Layers)
//             {
//                 var wanted = layer switch
//                 {
//                     HumanoidVisualLayers.Hair => comp.Hair,
//                     HumanoidVisualLayers.FacialHair => comp.FacialHair,
//                     _ => null,
//                 };
//
//                 if (wanted is not { } markingId || !_protos.TryIndex(markingId, out var proto))
//                     continue;
//
//                 if (!appearance.Markings.TryGetValue(organ, out var layers))
//                 {
//                     layers = new();
//                     appearance.Markings[organ] = layers;
//                 }
//
//                 layers[layer] = [proto.AsMarking().WithColor(comp.HairColor)];
//             }
//         }
//     }
// }
