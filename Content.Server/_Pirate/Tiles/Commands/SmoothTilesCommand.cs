// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;

namespace Content.Server._Pirate.Tiles.Commands;

/// <summary>
/// Recalculates smoothing variants for every tile on a grid. Only needed for maps that were saved before
/// their tiles could smooth, tiles placed in game are handled by <see cref="TileSmoothingSystem"/>.
/// </summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed class SmoothTilesCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "smoothtiles";

    public string Description => "Recalculates tile smoothing variants on a grid.";

    public string Help => $"Usage: {Command} <gridNetEntity>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netEnt) || !_entManager.TryGetEntity(netEnt, out var uid))
        {
            shell.WriteError($"Failed to parse entity '{args[0]}'.");
            return;
        }

        if (!_entManager.TryGetComponent(uid, out MapGridComponent? grid))
        {
            shell.WriteError($"Entity '{args[0]}' is not a grid.");
            return;
        }

        _entManager.System<TileSmoothingSystem>().UpdateGrid((uid.Value, grid));
        shell.WriteLine($"Resmoothed tiles on {args[0]}.");
    }
}
