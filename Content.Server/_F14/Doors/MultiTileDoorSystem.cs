using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._F14.Doors;
using Content.Shared.Doors.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._F14.Doors;

/// <summary>
/// Keeps the invisible blockers of a <see cref="MultiTileDoorComponent"/> spawned, anchored and in
/// sync with the door itself.
/// </summary>
public sealed class MultiTileDoorSystem : EntitySystem
{
    [Dependency] private readonly AirtightSystem _airtight = default!;
    [Dependency] private readonly OccluderSystem _occluder = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MultiTileDoorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MultiTileDoorComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
        SubscribeLocalEvent<MultiTileDoorComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<MultiTileDoorComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMove(Entity<MultiTileDoorComponent> ent, ref MoveEvent args)
    {
        if (args.OldRotation == args.NewRotation || ent.Comp.Blockers.Count == 0)
            return;

        DeleteBlockers(ent);
        SpawnBlockers(ent);
    }

    private void OnMapInit(Entity<MultiTileDoorComponent> ent, ref MapInitEvent args)
    {
        SpawnBlockers(ent);
    }

    private void OnAnchorStateChanged(Entity<MultiTileDoorComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            DeleteBlockers(ent);
        else if (LifeStage(ent) >= EntityLifeStage.MapInitialized)
            SpawnBlockers(ent);
    }

    private void OnShutdown(Entity<MultiTileDoorComponent> ent, ref ComponentShutdown args)
    {
        DeleteBlockers(ent);
    }

    private void SpawnBlockers(Entity<MultiTileDoorComponent> ent)
    {
        if (ent.Comp.Blockers.Count > 0)
            return;

        var xform = Transform(ent);
        if (!xform.Anchored || xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        // Match standard door collision initialization.
        var blocked = !TryComp<DoorComponent>(ent, out var door)
                      || door.State == DoorState.Closed
                      || door.State == DoorState.Closing && door.Partial
                      || door.State == DoorState.Opening && !door.Partial;
        var origin = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);

        foreach (var offset in ent.Comp.Offsets)
        {
            var tile = origin + MultiTileDoorComponent.Rotate(offset, xform.LocalRotation);
            var blocker = Spawn(ent.Comp.Blocker, _map.GridTileToLocal(gridUid, grid, tile));

            if (!_transform.AnchorEntity((blocker, Transform(blocker)), (gridUid, grid), tile))
            {
                QueueDel(blocker);
                continue;
            }

            ent.Comp.Blockers.Add(blocker);
        }

        SetBlocked(ent, blocked);
    }

    private void DeleteBlockers(Entity<MultiTileDoorComponent> ent)
    {
        foreach (var blocker in ent.Comp.Blockers)
        {
            QueueDel(blocker);
        }

        ent.Comp.Blockers.Clear();
    }

    /// <summary>
    /// Updates blocker airtightness and occlusion.
    /// </summary>
    public void SetBlocked(Entity<MultiTileDoorComponent> ent, bool blocked)
    {
        foreach (var blocker in ent.Comp.Blockers)
        {
            if (TerminatingOrDeleted(blocker))
                continue;

            if (TryComp<AirtightComponent>(blocker, out var airtight))
                _airtight.SetAirblocked((blocker, airtight), blocked);

            _occluder.SetEnabled(blocker, blocked);
        }
    }
}
