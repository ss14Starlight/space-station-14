using Content.Shared._Funkystation.BloodCult.Components;
using Content.Shared.Movement.Events;

namespace Content.Server._Funkystation.BloodCult.EntitySystems;

/// <summary>
/// Handles inactive juggernauts - prevents movement when soulstone is ejected
/// </summary>
public sealed class InactiveJuggernautSystem : EntitySystem
{

	public override void Initialize()
	{
		base.Initialize();
		
		SubscribeLocalEvent<JuggernautComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
	}

	private void OnUpdateCanMove(Entity<JuggernautComponent> juggernaut, ref UpdateCanMoveEvent args)
	{
		// Prevent movement if inactive (no soulstone)
		if (juggernaut.Comp.IsInactive)
		{
			args.Cancel();
		}
	}
}

