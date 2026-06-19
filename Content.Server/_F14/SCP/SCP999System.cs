using System.Collections.Generic;
using Content.Shared._F14.SCP;
using Content.Shared.Popups;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.Chat.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Player;
using Robust.Shared.Audio.Systems;

namespace Content.Server._F14.SCP;

public sealed class SCP999System : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SCP999Component, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            comp.Accumulator += frameTime;

            if (comp.Accumulator < comp.Cooldown)
                continue;

            comp.Accumulator = 0f;

            var targets = _lookup.GetEntitiesInRange(xform.Coordinates, 1.2f);
            var validTargets = new List<EntityUid>();

            foreach (var target in targets)
            {
                if (target == uid) continue;
                if (!HasComp<MobStateComponent>(target)) continue;
                if (!_mobState.IsAlive(target)) continue;

                validTargets.Add(target);
            }

            if (validTargets.Count == 0)
                continue;

            var chosen = _random.Pick(validTargets);


            if (_random.Prob(0.5f))
            {
                _popup.PopupEntity("SCP-999 starts tickling you!", chosen, chosen, PopupType.Large);
                _popup.PopupEntity($"{Name(chosen)} is being tickled by SCP-999!", chosen, Filter.PvsExcept(chosen), true);

                _chat.TryEmoteWithChat(chosen, "Laugh");
            }
            else
            {
                _popup.PopupEntity("SCP-999 gives you a warm, gelatinous hug!", chosen, chosen, PopupType.Medium);
                _popup.PopupEntity($"SCP-999 gently hugs {Name(chosen)}!", chosen, Filter.PvsExcept(chosen), true);

            }
        }
    }
}