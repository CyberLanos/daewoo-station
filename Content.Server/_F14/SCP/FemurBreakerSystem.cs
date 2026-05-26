using Content.Server._F14.SCP;
using Content.Shared._F14.SCP;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs; 
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Player;

namespace Content.Server._F14.SCP.FemurBreaker;

public sealed class FemurBreakerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem    _audio      = default!;
    [Dependency] private readonly TransformSystem      _transform  = default!;
    [Dependency] private readonly SCP106System         _scp106     = default!;
    [Dependency] private readonly EntityLookupSystem   _lookup     = default!;
    [Dependency] private readonly MobStateSystem       _mobState   = default!;
    [Dependency] private readonly SharedPopupSystem    _popup      = default!;
    [Dependency] private readonly AppearanceSystem     _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FemurBreakerComponent, ActivateInWorldEvent>(OnActivate);

        SubscribeLocalEvent<FemurBreakerComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
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
    }

    private void OnActivate(EntityUid uid, FemurBreakerComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (comp.Used)
        {
            _popup.PopupEntity(
                Loc.GetString("femur-breaker-already-used"),
                uid, args.User, PopupType.Medium);
            args.Handled = true;
            return;
        }

        if (comp.Activating)
        {
            args.Handled = true;
            return;
        }

        if (!HasVictimInRange(uid, comp))
        {
            _popup.PopupEntity(
                Loc.GetString("femur-breaker-no-victim"),
                uid, args.User, PopupType.Medium);
            args.Handled = true;
            return;
        }

        comp.Activating      = true;
        comp.ActivationTimer = comp.ActivationDelay;

        _popup.PopupEntity(
            Loc.GetString("femur-breaker-activating"),
            uid, args.User, PopupType.Large);

        _appearance.SetData(uid, FemurBreakerVisuals.State, FemurBreakerState.Activating);

        args.Handled = true;
    }


    private void FireFemurBreaker(EntityUid uid, FemurBreakerComponent comp, TransformComponent xform)
    {
        comp.Used = true;

        _audio.PlayGlobal(
            comp.ActivationSound,
            Filter.Broadcast(),
            recordReplay: true);

        int summoned = 0;
        var scpQuery = EntityQueryEnumerator<SCP106Component>();
        while (scpQuery.MoveNext(out var scpUid, out var scpComp))
        {
            if (scpComp.IsSubmerged)
                _scp106.Emerge(scpUid, scpComp);

            _transform.SetCoordinates(scpUid, xform.Coordinates);
            summoned++;
        }

        _appearance.SetData(uid, FemurBreakerVisuals.State, FemurBreakerState.Used);

        Log.Info($"[FemurBreaker] Fired at {xform.Coordinates}, summoned {summoned} SCP-106 instance(s).");
    }

    private bool HasVictimInRange(EntityUid uid, FemurBreakerComponent comp)
    {
        foreach (var near in _lookup.GetEntitiesInRange(uid, comp.VictimRange))
        {
            if (near == uid)
                continue;
            if (HasComp<SCP106Component>(near))
                continue;
            if (!TryComp<MobStateComponent>(near, out var mobState))
                continue;
            if (!_mobState.IsAlive(near, mobState))
                continue;

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
            Text     = Loc.GetString("femur-breaker-reset-verb"),
            Act      = () =>
            {
                comp.Used       = false;
                comp.Activating = false;
                _appearance.SetData(uid, FemurBreakerVisuals.State, FemurBreakerState.Idle);

                _popup.PopupEntity(
                    Loc.GetString("femur-breaker-reset-done"),
                    uid, args.User, PopupType.Medium);
            },
            Priority = 1,
        };

        args.Verbs.Add(resetVerb);
    }
}