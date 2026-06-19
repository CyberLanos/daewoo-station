using Content.Shared._F14.SCP;
using Content.Shared.Interaction;
using Content.Shared.Humanoid;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Server.Chat.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Enums;
using System;

namespace Content.Server._F14.SCP;

public sealed class SCP113System : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoidAppearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SCP113Component, InteractHandEvent>(OnPickupAttempt);
    }

    private void OnPickupAttempt(EntityUid uid, SCP113Component comp, InteractHandEvent args)
    {
        var user = args.User;

        if (!TryComp<HumanoidAppearanceComponent>(user, out var humanoid))
            return;

        var newSex = humanoid.Sex == Sex.Male ? Sex.Female : Sex.Male;
        _humanoidAppearance.SetSex(user, newSex, true);

        if (newSex == Sex.Female)
            humanoid.Gender = Gender.Female;
        else
            humanoid.Gender = Gender.Male;

        var newLayers = new System.Collections.Generic.Dictionary<HumanoidVisualLayers, string>();

        foreach (var (layer, info) in humanoid.CustomBaseLayers)
        {
            if (layer != HumanoidVisualLayers.Chest && layer != HumanoidVisualLayers.Head)
                continue;

            if (info.Id == null)
                continue;

            string id = info.Id.ToString() ?? "";

            if (string.IsNullOrEmpty(id))
                continue;

            string newId = id;

            if (newSex == Sex.Female)
            {
                if (id.EndsWith("Male"))
                    newId = id.Substring(0, id.Length - 4) + "Female";
                else if (id.EndsWith("male"))
                    newId = id.Substring(0, id.Length - 4) + "female";
                else if (!id.EndsWith("Female") && !id.EndsWith("female"))
                    newId = id + "Female";
            }
            else
            {
                // Якщо стаємо чоловіком
                if (id.EndsWith("Female"))
                    newId = id.Substring(0, id.Length - 6);
                else if (id.EndsWith("female"))
                    newId = id.Substring(0, id.Length - 6);
            }

            if (newId != id)
            {
                newLayers[layer] = newId;
            }
        }

        foreach (var (layer, newId) in newLayers)
        {
            try
            {
                _humanoidAppearance.SetBaseLayerId(user, layer, newId, true, humanoid);
            }
            catch (Exception)
            {
            }
        }

        Dirty(user, humanoid);

        _stun.TryKnockdown(user, TimeSpan.FromSeconds(4), true);

        if (TryComp<DamageableComponent>(user, out var damageable))
        {
            var healDamage = new DamageSpecifier();
            foreach (var (type, amount) in damageable.Damage.DamageDict)
            {
                healDamage.DamageDict.Add(type, -amount);
            }
            _damageable.TryChangeDamage(user, healDamage, true);
        }
        _chat.TryEmoteWithChat(user, "Scream");

        _popup.PopupEntity("The stone shocks your body! You fall unconscious as your DNA completely rewrites itself!", user, user, PopupType.LargeCaution);
        _popup.PopupEntity($"{Name(user)} touches {Name(uid)}, screams in agony, and collapses as their body morphs!", user, Filter.PvsExcept(user), true);
    }
}