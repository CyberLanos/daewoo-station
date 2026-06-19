using System;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Atmos.Rotting;
using Content.Shared.NPC.Systems;
using Content.Shared.Zombies;
using Content.Server.Zombies;
using Content.Shared._F14.SCP;
using Content.Shared.Verbs;
using Content.Shared.DoAfter;
using Robust.Shared.Player;
using Robust.Shared.GameObjects;

namespace Content.Server._F14.SCP;

public sealed class SCP049System : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ZombieSystem _zombie = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZombieComponent, DamageModifyEvent>(OnZombieDamageModify);
        SubscribeLocalEvent<SCP049Component, GetVerbsEvent<InnateVerb>>(AddCureVerb);
        SubscribeLocalEvent<SCP049Component, SCP049CureDoAfterEvent>(OnCureDoAfter);
    }

    private void OnZombieDamageModify(EntityUid uid, ZombieComponent comp, DamageModifyEvent args)
    {
        if (args.Origin != null && HasComp<SCP049Component>(args.Origin.Value))
        {
            args.Damage = new DamageSpecifier();
        }
    }

    private void AddCureVerb(EntityUid uid, SCP049Component comp, GetVerbsEvent<InnateVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var target = args.Target;

        if (uid == target)
            return;

        if (!TryComp<MobStateComponent>(target, out var mobState) ||
            !_mobState.IsDead(target, mobState) ||
            HasComp<RottingComponent>(target) ||
            HasComp<ZombieComponent>(target))
        {
            return;
        }

        InnateVerb verb = new()
        {
            Text = "Cure Patient",
            Act = () =>
            {
                _popup.PopupEntity("Time to start operation... it will take some time.", uid, uid);

                var doAfterArgs = new DoAfterArgs(EntityManager, uid, 30f, new SCP049CureDoAfterEvent(), uid, target: target)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true
                };

                _doAfter.TryStartDoAfter(doAfterArgs);
            }
        };

        args.Verbs.Add(verb);
    }

    private void OnCureDoAfter(EntityUid uid, SCP049Component comp, SCP049CureDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Args.Target == null)
            return;

        EntityUid targetUid = args.Args.Target.Value;

        args.Handled = true;

        if (TryComp<DamageableComponent>(targetUid, out var damage))
        {
            var heal = new DamageSpecifier();
            foreach (var (type, amount) in damage.Damage.DamageDict)
            {
                heal.DamageDict.Add(type, -amount);
            }
            _damageable.TryChangeDamage(targetUid, heal, true);
        }

        if (TryComp<MobStateComponent>(targetUid, out var mobStateTarget))
            _mobState.ChangeMobState(targetUid, MobState.Alive, mobStateTarget);

        _zombie.ZombifyEntity(targetUid);

        _faction.ClearFactions(targetUid);
        _faction.AddFaction(targetUid, "SCP");

        _popup.PopupEntity("I succeded, wake up my cured friend!", uid, uid);
    }
}