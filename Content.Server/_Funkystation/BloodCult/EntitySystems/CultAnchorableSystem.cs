using Robust.Shared.GameObjects;
using Content.Shared._Funkystation.BloodCult;
using Content.Shared._Funkystation.BloodCult.Components;

namespace Content.Server._Funkystation.BloodCult.EntitySystems;

public sealed partial class CultAnchorableSystem : EntitySystem
{
	[Dependency] private readonly SharedAppearanceSystem _appearance = default!;

	public override void Initialize()
	{
		base.Initialize();

		SubscribeLocalEvent<CultAnchorableComponent, AnchorStateChangedEvent>(OnAnchorChanged);
	}

	private void OnAnchorChanged(EntityUid uid, CultAnchorableComponent component, AnchorStateChangedEvent args)
	{
		if (args.Anchored)
			_appearance.SetData(uid, CultStructureVisuals.Anchored, true);
		else
			_appearance.SetData(uid, CultStructureVisuals.Anchored, false);
	}
}
