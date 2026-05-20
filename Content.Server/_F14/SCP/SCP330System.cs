using System;
using System.Collections.Generic;
using Content.Server.Body.Systems;      
using Content.Shared.Body.Components;  
using Content.Shared.Body.Part;        
using Content.Shared.Hands.Components;   
using Content.Shared.Hands.EntitySystems; 
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events; 
using Content.Shared.Popups;
using Content.Shared.Damage; 
using Content.Shared.Nutrition; 
using Content.Server.Explosion.EntitySystems; 
using Content.Shared._F14.SCP;
using Robust.Shared.GameObjects;
using Robust.Shared.Containers; 
using Robust.Shared.Random; 

namespace Content.Server._F14.SCP;

public sealed class SCP330System : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!; 
    [Dependency] private readonly DamageableSystem _damageable = default!; 
    [Dependency] private readonly IRobustRandom _random = default!; 
    [Dependency] private readonly ExplosionSystem _explosion = default!; 

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<SCP330Component, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<SCP330PinkCandyComponent, UseInHandEvent>(OnPinkCandyUsed);
    }

    private void OnInteractHand(EntityUid uid, SCP330Component comp, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;

        if (!TryComp<HandsComponent>(user, out var handsComp) || handsComp.Hands.Count == 0)
        {
            _popup.PopupEntity("You dont have hands to take candy, you should be concernd!", uid, user, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        if (!comp.TakenCount.TryGetValue(user, out var count))
        {
            count = 0;
        }

        count++;
        comp.TakenCount[user] = count;

        if (count > 2)
        {
            _popup.PopupEntity("You decided to take one more candy... Suddenly your hand fallen off!!", user, user, PopupType.LargeCaution);
            AmputateHands(user);
            return;
        }

        var candyIndex = _random.Next(1, 8);
        var candyUid = EntityManager.SpawnEntity($"SCP330Candy{candyIndex}", Transform(user).Coordinates);

        if (!_hands.TryPickupAnyHand(user, candyUid, handsComp: handsComp))
        {
            _popup.PopupEntity("Your hands are full, candy sliped from your hand.", uid, user);
        }
        else
        {
            _popup.PopupEntity("You took sweat looking candy, note on a tray says - take no more than two, please!!.", uid, user);
        }
    }

    private void OnPinkCandyUsed(EntityUid uid, SCP330PinkCandyComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true; 
        var user = args.User;
        
        _popup.PopupEntity("It tastes like... oh no---", user, user, PopupType.LargeCaution);

        _explosion.QueueExplosion(user, "Default", 1000f, 20f, 200f); 
        
        QueueDel(uid);
    }

    private void AmputateHands(EntityUid user)
    {
        if (!TryComp<BodyComponent>(user, out var body))
            return;

        var handsToDrop = new List<EntityUid>();

        foreach (var (partId, partComp) in _body.GetBodyChildren(user, body))
        {
            if (partComp.PartType == BodyPartType.Hand)
            {
                handsToDrop.Add(partId);
            }
        }

        foreach (var handId in handsToDrop)
        {
            _container.TryRemoveFromContainer(handId, force: true);
        }

        _bloodstream.TryModifyBleedAmount(user, 30f);
    }
}