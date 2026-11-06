// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Parallax;
using Content.Client.Parallax.Data;
using Content.Client.Parallax.Managers;
using Content.Shared._Pirate.CCVars;
using Content.Shared._Pirate.Light;
using Content.Shared._Pirate.ZLevels.Core;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Light;

/// <summary>
/// SS13-style starlight for space maps. SS14 space gives no light: a space map has no MapLightComponent, so its
/// ambient is black, and the parallax only looks bright because it is drawn unshaded. This fills the light map
/// with starlight and roofs the grids, the same way planets do it with MapLight + ImplicitRoof, except that tiles
/// which look down into open space (lattice, glass floors) stay open, and tinted glass colours the light.
/// Colour and strength per map: the map's <see cref="StarlightComponent"/>, else the parallax that is drawn
/// (also mid-transition, so a Journey warp fades it), else the server CVars.
/// </summary>
public sealed class StarlightSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IParallaxManager _parallaxManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ParallaxSystem _parallax = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;

    /// <summary>
    /// Decks move and lower decks change without a tile change on this grid, so results also expire.
    /// </summary>
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(1);

    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), bool> _openColumn = new();
    private List<Entity<MapGridComponent>> _gridScratch = new();
    private TimeSpan _cacheExpiry;
    private StarlightOverlay? _overlayInstance;

    private Color _defaultColor = Color.FromHex("#8589FA");
    private float _defaultStrength;

    public bool Enabled { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        Subs.CVar(_cfg, PirateVars.Starlight, SetEnabled, true);
        Subs.CVar(_cfg, PirateVars.StarlightStrength, v => _defaultStrength = Math.Clamp(v, 0f, 1f), true);
        Subs.CVar(_cfg, PirateVars.StarlightColor, v =>
        {
            if (Color.TryFromHex(v, out var color))
                _defaultColor = color;
            else
                Log.Warning($"pirate.starlight_color '{v}' is not a hex colour, keeping {_defaultColor.ToHex()}.");
        }, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        SetEnabled(false);
        _openColumn.Clear();
    }

    private void SetEnabled(bool enabled)
    {
        Enabled = enabled;

        if (enabled && _overlayInstance == null)
        {
            _overlayInstance = new StarlightOverlay(EntityManager, this);
            _overlay.AddOverlay(_overlayInstance);
        }
        else if (!enabled && _overlayInstance != null)
        {
            _overlay.RemoveOverlay(_overlayInstance);
            _overlayInstance = null;
        }
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        // A change on any deck can open or close a column above it.
        _openColumn.Clear();
    }

    /// <summary>
    /// Whether this map gets starlight: a space map. Planets (the map is a grid) and maps with their own ambient
    /// light (MapLightComponent: planets, Lavaland, arenas) keep their own lighting.
    /// </summary>
    public bool IsStarlit(EntityUid mapUid)
    {
        return mapUid.IsValid() && !HasComp<MapGridComponent>(mapUid) && !HasComp<MapLightComponent>(mapUid);
    }

    /// <summary>
    /// Starlight colour and strength of a map. See the class summary for the order.
    /// </summary>
    public (Color Color, float Strength) GetStarlight(EntityUid mapUid)
    {
        var (color, strength) = GetParallaxStarlight(mapUid);

        if (TryComp<StarlightComponent>(mapUid, out var comp))
        {
            color = comp.Color ?? color;
            strength = comp.Strength ?? strength;
        }

        return (color, Math.Clamp(strength, 0f, 1f));
    }

    private (Color Color, float Strength) GetParallaxStarlight(EntityUid mapUid)
    {
        // Same parallax the sky draws: a running fade, else the settled override, else the map's own.
        var progress = _parallaxManager.GetTransitionProgress();

        if (progress >= 0f
            && _parallaxManager.GetTransitionFrom() is { } from
            && _parallaxManager.GetTransitionTo() is { } to)
        {
            var a = SampleParallax(from);
            var b = SampleParallax(to);
            return (Color.InterpolateBetween(a.Color, b.Color, progress), a.Strength + (b.Strength - a.Strength) * progress);
        }

        return SampleParallax(_parallaxManager.GetCurrent() ?? _parallax.GetParallax(mapUid));
    }

    private (Color Color, float Strength) SampleParallax(string parallax)
    {
        if (!_proto.TryIndex<ParallaxPrototype>(parallax, out var proto))
            return (_defaultColor, _defaultStrength);

        return (proto.StarlightColor ?? _defaultColor, proto.StarlightStrength ?? _defaultStrength);
    }

    /// <summary>
    /// The light under a grid tile: null for a roofed tile (dark), otherwise the starlight colour that reaches it.
    /// Tinted glass replaces the colour with its own (as SS13 plasma glass does) at the map's strength.
    /// </summary>
    public Color? GetTileLight(Entity<MapGridComponent> grid, TileRef tile, Color starlight, float strength)
    {
        if (_tileDefs[tile.Tile.TypeId] is not ContentTileDefinition { ZTransparent: true } def)
            return null;

        if (!IsColumnOpen(grid, tile.GridIndices))
            return null;

        if (def.StarlightColor is not { } tint)
            return starlight;

        return new Color(tint.R * strength, tint.G * strength, tint.B * strength);
    }

    private bool IsColumnOpen(Entity<MapGridComponent> grid, Vector2i indices)
    {
        if (_timing.RealTime > _cacheExpiry)
        {
            _openColumn.Clear();
            _cacheExpiry = _timing.RealTime + CacheLifetime;
        }

        var key = (grid.Owner, indices);

        if (_openColumn.TryGetValue(key, out var open))
            return open;

        open = CheckColumn(grid, indices);
        _openColumn[key] = open;
        return open;
    }

    private bool CheckColumn(Entity<MapGridComponent> grid, Vector2i indices)
    {
        if (Transform(grid).MapUid is not { } current)
            return false;

        var center = _maps.GridTileToWorldPos(grid, grid.Comp, indices);

        while (_zLevels.TryMapDown(current, out var below))
        {
            current = below.Value.Owner;

            if (HasFloorAt(current, center))
                return false;
        }

        // The bottom of the stack must be space too, not a planet surface.
        return IsStarlit(current);
    }

    private bool HasFloorAt(EntityUid mapUid, Vector2 worldPos)
    {
        _gridScratch.Clear();
        _maps.FindGridsIntersecting(mapUid, Box2.CenteredAround(worldPos, new Vector2(0.1f)), ref _gridScratch);

        foreach (var grid in _gridScratch)
        {
            var indices = _maps.WorldToTile(grid, grid.Comp, worldPos);

            if (_maps.TryGetTile(grid.Comp, indices, out var tile)
                && !CMUZLevelOpeningCache.IsVisualOpeningTile(tile, _tileDefs))
            {
                return true;
            }
        }

        return false;
    }
}
