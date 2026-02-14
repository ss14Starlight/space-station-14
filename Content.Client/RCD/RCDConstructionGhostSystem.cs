using Content.Client.Hands.Systems;
using Content.Shared.Interaction;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
// Starlight Start
using Robust.Shared.Input;
using Content.Client._Starlight.RCD;
using Robust.Shared.Input.Binding;
using Content.Client.Atmos;
using Content.Shared.Input;
// Starlight End

namespace Content.Client.RCD;

/// <summary>
/// System for handling structure ghost placement in places where RCD can create objects.
/// </summary>
public sealed class RCDConstructionGhostSystem : EntitySystem
{
    private const string PlacementMode = nameof(AlignRCDConstruction);
    private const string RpdPlacementMode = nameof(AlignRPDAtmosPipeLayers); // Starlight RPD

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPlacementManager _placementManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly HandsSystem _hands = default!;

    private Direction _placementDirection = default;
    // Starlight Start: RPD
    private bool _useMirrorPrototype = false;
    public event EventHandler? FlipConstructionPrototype;

    public override void Initialize()
    {
        base.Initialize();

        // bind key
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.EditorFlipObject,
                new PointerInputCmdHandler(HandleFlip, outsidePrediction: true))
            .Register<RCDConstructionGhostSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<RCDConstructionGhostSystem>();
        base.Shutdown();
    }

    private bool HandleFlip(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (args.State == BoundKeyState.Down)
        {
            if (!_placementManager.IsActive || _placementManager.Eraser)
                return false;

            var placerEntity = _placementManager.CurrentPermission?.MobUid;

            if (!TryComp<RCDComponent>(placerEntity, out var rcd) ||
                string.IsNullOrEmpty(rcd.CachedPrototype.MirrorPrototype))
                return false;

            _useMirrorPrototype = !rcd.UseMirrorPrototype;

            // tell the server

            RaiseNetworkEvent(new RCDConstructionGhostFlipEvent(GetNetEntity(placerEntity.Value), _useMirrorPrototype));
        }

        return true;
    }
    // Starlight End: RPD

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Get current placer data
        var placerEntity = _placementManager.CurrentPermission?.MobUid;
        var placerProto = _placementManager.CurrentPermission?.EntityType;
        var placerIsRCD = HasComp<RCDComponent>(placerEntity);

        // Exit if erasing or the current placer is not an RCD (build mode is active)
        if (_placementManager.Eraser || (placerEntity != null && !placerIsRCD))
            return;

        // Determine if player is carrying an RCD in their active hand
        if (_playerManager.LocalSession?.AttachedEntity is not { } player)
            return;

        var heldEntity = _hands.GetActiveItem(player);

        // Don't open the placement overlay for client-side RCDs.
        // This may happen when predictively spawning one in your hands.
        if (heldEntity != null && IsClientSide(heldEntity.Value))
            return;

        if (!TryComp<RCDComponent>(heldEntity, out var rcd))
        {
            // If the player was holding an RCD, but is no longer, cancel placement
            if (placerIsRCD)
                _placementManager.Clear();

            return;
        }
        // Starlight edit Start: RPD - use the mirrored prototype if the flip state is toggled on
        // var prototype = _protoManager.Index(rcd.ProtoId);

        // Determine if mirrored
        var cachedProto = rcd.CachedPrototype;
        var wantMirror = _useMirrorPrototype && !string.IsNullOrEmpty(cachedProto.MirrorPrototype);
        var prototype = wantMirror ? cachedProto.MirrorPrototype : cachedProto.Prototype;

        bool isLayered = rcd.IsRpd
            && _protoManager.TryIndex<RCDPrototype>(cachedProto.ID, out var rcdProto)
            && rcdProto.HasLayers;

        var desiredMode = isLayered ? RpdPlacementMode : PlacementMode;
        // Starlight edit End: RPD - use the mirrored prototype if the flip state is toggled on

        // Update the direction the RCD prototype based on the placer direction
        if (_placementDirection != _placementManager.Direction)
        {
            _placementDirection = _placementManager.Direction;
            RaiseNetworkEvent(new RCDConstructionGhostRotationEvent(GetNetEntity(heldEntity.Value), _placementDirection));
        }

        // If the placer has not changed, exit
        // Starlight edit Start
        if (heldEntity == placerEntity &&
            prototype == placerProto &&
            _placementManager.CurrentPermission?.PlacementOption == desiredMode)
        // Starlight edit End
            return;

        // Create a new placer
        var newObjInfo = new PlacementInformation
        {
            MobUid = heldEntity.Value,
            PlacementOption = desiredMode, // Starlight Edit: PlacementMode -> desiredMode
            EntityType = prototype, // Starlight Edit: prototype.Prototype -> prototype
            Range = (int)Math.Ceiling(SharedInteractionSystem.InteractionRange),
            IsTile = (cachedProto.Mode == RcdMode.ConstructTile), // Starlight Edit: prototype.Mode -> cachedProto.Mode
            UseEditorContext = false,
        };

        _placementManager.Clear();
        _placementManager.BeginPlacing(newObjInfo);
    }
}
