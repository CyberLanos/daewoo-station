// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.ServerCurrency;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Sandbox;
using Content.Shared._Pirate.CCVars;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.GameTicking;
using Content.Shared.Mind.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;

namespace Content.Pirate.Server.ServerCurrency;

/// <summary>
/// Goobcoin payouts that happen during the round rather than at round end: a bonus for readying up
/// and spawning at round start, and a penalty for cryosleeping out of the round early.
/// </summary>
/// <remarks>
/// The round-end payout lives in Goobstation's ServerCurrencySystem. This only adds to it; balance
/// changes made here go through <see cref="ICommonCurrencyManager"/> so that system's popup and
/// client balance update still fire.
/// </remarks>
public sealed class PirateGoobcoinRewardSystem : EntitySystem
{
    [Dependency] private readonly ICommonCurrencyManager _currency = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly SandboxSystem _sandbox = default!;

    private int _roundStartBonus;
    private int _earlyCryoPenalty;
    private float _earlyCryoWindowMinutes;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<CryostorageComponent, EntInsertedIntoContainerMessage>(OnInsertedIntoCryostorage);
        SubscribeLocalEvent<CryostorageContainedComponent, MindRemovedMessage>(OnCryoMindRemoved);

        Subs.CVar(_cfg, PirateGoobcoinCVars.RoundStartBonus, value => _roundStartBonus = value, true);
        Subs.CVar(_cfg, PirateGoobcoinCVars.EarlyCryoPenalty, value => _earlyCryoPenalty = value, true);
        Subs.CVar(_cfg, PirateGoobcoinCVars.EarlyCryoWindowMinutes, value => _earlyCryoWindowMinutes = value, true);
    }

    /// <summary>
    /// Pays the round-start bonus. <see cref="PlayerSpawnCompleteEvent.LateJoin"/> is only false for
    /// the round-start spawn loop, so this is exactly "readied up and got a job at round start".
    /// </summary>
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.LateJoin || _roundStartBonus <= 0 || _sandbox.IsSandboxEnabled)
            return;

        _currency.AddCurrency(ev.Player.UserId, _roundStartBonus);
        _chat.DispatchServerMessage(ev.Player,
            Loc.GetString("pirate-goobcoin-round-start-bonus",
                ("amount", _currency.Stringify(_roundStartBonus))));
    }

    private void OnInsertedIntoCryostorage(Entity<CryostorageComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        // Outside of a live round there is no meaningful round duration to compare against.
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        var entry = EnsureComp<PirateCryoEntryTimeComponent>(args.Entity);
        entry.RoundTimeOnEntry = _gameTicker.RoundDuration();
        entry.Charged = false;
    }

    /// <summary>
    /// Takes the early-cryo penalty. Losing the mind while inside a cryopod is the point of no
    /// return: cryostorage ghosts the player as it round-removes them, and a player who climbs back
    /// out during the grace period never gets here.
    /// </summary>
    private void OnCryoMindRemoved(Entity<CryostorageContainedComponent> ent, ref MindRemovedMessage args)
    {
        if (_earlyCryoPenalty <= 0 || _sandbox.IsSandboxEnabled)
            return;

        if (args.Mind.Comp.UserId is not { } userId)
            return;

        if (!TryComp<PirateCryoEntryTimeComponent>(ent.Owner, out var entry) || entry.Charged)
            return;

        if (entry.RoundTimeOnEntry.TotalMinutes > _earlyCryoWindowMinutes)
            return;

        entry.Charged = true;

        // Balances are not allowed to go negative; take whatever they can actually pay.
        var amount = Math.Min(_earlyCryoPenalty, _currency.GetBalance(userId));
        if (amount <= 0)
            return;

        _currency.RemoveCurrency(userId, amount);

        if (!_players.TryGetSessionById(userId, out var session))
            return;

        _chat.DispatchServerMessage(session,
            Loc.GetString("pirate-goobcoin-early-cryo-penalty",
                ("amount", _currency.Stringify(amount)),
                ("minutes", (int) _earlyCryoWindowMinutes)));
    }
}
