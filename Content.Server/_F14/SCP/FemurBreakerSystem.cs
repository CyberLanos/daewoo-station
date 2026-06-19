using Content.Server._F14.SCP;
using Content.Shared._F14.SCP;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Buckle.Components;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Damage;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Server._F14.SCP.FemurBreaker;

[RegisterComponent]
public sealed partial class FemurBreakerSummoningComponent : Component
{
    public EntityCoordinates TargetCoords;
    public float Timer = 2.5f;
}

public sealed class FemurBreakerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SCP106System _scp106 = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FemurBreakerComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<FemurBreakerComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<FemurBreakerComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnSignalReceived(EntityUid uid, FemurBreakerComponent comp, ref SignalReceivedEvent args)
    {
        TryActivate(uid, comp, null);
    }

    private void OnActivate(EntityUid uid, FemurBreakerComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled) return;

        if (TryActivate(uid, comp, args.User))
            args.Handled = true;
    }

    private bool TryActivate(EntityUid uid, FemurBreakerComponent comp, EntityUid? user)
    {
        if (comp.Used || comp.Activating)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("femur-breaker-already-used") ?? "Already used!", uid, user.Value, PopupType.Medium);
            return false;
        }

        if (!HasVictim(uid))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("femur-breaker-no-victim") ?? "Need a living human buckled!", uid, user.Value, PopupType.Medium);
            return false;
        }

        comp.Activating = true;
        comp.ActivationTimer = comp.ActivationDelay;

        if (user != null)
            _popup.PopupEntity(Loc.GetString("femur-breaker-activating") ?? "ACTIVATING...", uid, user.Value, PopupType.Large);

        _appearance.SetData(uid, FemurBreakerVisuals.State, FemurBreakerState.Activating);

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FemurBreakerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!comp.Activating)
                continue;

            comp.ActivationTimer -= frameTime;
            if (comp.ActivationTimer > 0f)
                continue;

            comp.Activating = false;
            FireFemurBreaker(uid, comp, xform);
        }

        var summonQuery = EntityQueryEnumerator<FemurBreakerSummoningComponent, SCP106Component>();
        while (summonQuery.MoveNext(out var sUid, out var summonComp, out var scpComp))
        {
            summonComp.Timer -= frameTime;
            if (summonComp.Timer <= 0f)
            {
                _transform.SetCoordinates(sUid, summonComp.TargetCoords);

                _scp106.Emerge(sUid, scpComp);

                RemComp<FemurBreakerSummoningComponent>(sUid);
            }
        }
    }

    private void FireFemurBreaker(EntityUid uid, FemurBreakerComponent comp, TransformComponent xform)
    {
        comp.Used = true;

        _audio.PlayGlobal(
            comp.ActivationSound,
            Filter.Broadcast(),
            recordReplay: true);

        if (TryComp<StrapComponent>(uid, out var strap))
        {
            foreach (var buckled in strap.BuckledEntities)
            {
                var dmg = new DamageSpecifier();
                dmg.DamageDict.Add("Blunt", 300);
                _damageable.TryChangeDamage(buckled, dmg, true, origin: uid);
            }
        }

        int summoned = 0;
        var scpQuery = EntityQueryEnumerator<SCP106Component>();
        while (scpQuery.MoveNext(out var scpUid, out var scpComp))
        {
            if (!scpComp.IsSubmerged)
                _scp106.Submerge(scpUid, scpComp);

            var adjacentCoords = new EntityCoordinates(xform.Coordinates.EntityId, xform.Coordinates.Position + new Vector2(1.5f, 0f));

            var summonComp = EnsureComp<FemurBreakerSummoningComponent>(scpUid);
            summonComp.TargetCoords = adjacentCoords;
            summonComp.Timer = 2.5f;

            summoned++;
        }

        _appearance.SetData(uid, FemurBreakerVisuals.State, FemurBreakerState.Used);

        Log.Info($"[FemurBreaker] Fired at {xform.Coordinates}, summoned {summoned} SCP-106 instance(s).");
    }

    private bool HasVictim(EntityUid uid)
    {
        if (!TryComp<StrapComponent>(uid, out var strap))
            return false;

        foreach (var buckled in strap.BuckledEntities)
        {
            if (HasComp<SCP106Component>(buckled))
                continue;

            if (TryComp<MobStateComponent>(buckled, out var mobState) && _mobState.IsAlive(buckled, mobState))
                return true;
        }
        return false;
    }

    private void OnGetVerbs(EntityUid uid, FemurBreakerComponent comp, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        if (!comp.Used)
            return;

        var resetVerb = new Verb
        {
            Text = Loc.GetString("femur-breaker-reset-verb") ?? "Reset",
            Act = () =>
            {
                comp.Used = false;
                comp.Activating = false;
                _appearance.SetData(uid, FemurBreakerVisuals.State, FemurBreakerState.Idle);

                _popup.PopupEntity(
                    Loc.GetString("femur-breaker-reset-done") ?? "Machine reset",
                    uid, args.User, PopupType.Medium);
            },
            Priority = 1,
        };

        args.Verbs.Add(resetVerb);
    }
}