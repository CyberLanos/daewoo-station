using Content.Shared._F14.SCP;
using Content.Shared.Doors.Systems; 
using Content.Shared.Doors.Components; 
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using System.Linq;
using System;

namespace Content.Server._F14.Security;

public sealed class KeylockSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedDoorSystem _doorSystem = default!; 
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<KeylockComponent, KeylockAttemptMessage>(OnKeypadAttempt);
    }

    private void OnKeypadAttempt(EntityUid uid, KeylockComponent comp, KeylockAttemptMessage args)
    {
        if (comp.LockedUntil.HasValue)
        {
            if (_timing.CurTime < comp.LockedUntil.Value)
            {
                return; 
            }
            else
            {
                comp.FailedAttempts = 0;
                comp.LockedUntil = null;
                Dirty(uid, comp);
            }
        }

        if (args.AttemptedCode == comp.Code)
        {
            comp.IsLocked = false;
            comp.FailedAttempts = 0;
            comp.LockedUntil = null;
            Dirty(uid, comp);

            bool doorOpened = false;

            if (TryComp<KeylockAccessComponent>(uid, out var access) && access.LinkedDoor.HasValue)
            {
                _doorSystem.TryOpen(access.LinkedDoor.Value);
                doorOpened = true;
            }
            
            if (!doorOpened)
            {
                var coords = Transform(uid).Coordinates;
                var lookup = EntityManager.System<EntityLookupSystem>();
                
                var nearbyEntities = lookup.GetEntitiesInRange(coords, 2.0f);

                foreach (var ent in nearbyEntities)
                {
                    if (HasComp<DoorComponent>(ent))
                    {
                        _doorSystem.TryOpen(ent);
                    }
                }
            }
        }
        else
        {
            comp.FailedAttempts++;
            Dirty(uid, comp);

            if (comp.FailedAttempts >= comp.MaxAttempts)
            {
                comp.LockedUntil = _timing.CurTime + comp.LockoutDuration;
            }
        }

        UpdateBuiState(uid, comp);
    }

    private void UpdateBuiState(EntityUid uid, KeylockComponent comp)
    {
        _uiSystem.SetUiState(uid, KeylockUiKey.Key, new KeylockBuiState(comp.IsLocked, comp.FailedAttempts, comp.MaxAttempts));
    }

    public void SetCode(EntityUid uid, string newCode, KeylockComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        if (newCode.Length == comp.CodeLength && newCode.All(char.IsDigit))
        {
            comp.Code = newCode;
            Dirty(uid, comp);
        }
    }
}