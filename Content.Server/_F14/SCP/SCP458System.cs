using Content.Shared._F14.SCP;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server._F14.SCP;

public sealed class SCP458System : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SCP458Component, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SCP458Component, BoundUIClosedEvent>(OnUIClosed);
    }

    private void OnMapInit(EntityUid uid, SCP458Component comp, MapInitEvent args)
    {
        RegeneratePizza(uid, comp);
    }

    private void OnUIClosed(EntityUid uid, SCP458Component comp, BoundUIClosedEvent args)
    {
        RegeneratePizza(uid, comp);
    }

    private void RegeneratePizza(EntityUid uid, SCP458Component comp)
    {
        if (_container.TryGetContainer(uid, "storagebase", out var container))
        {
            if (container.ContainedEntities.Count == 0)
            {
                if (comp.PizzaPrototypes.Count == 0)
                    return;

                var randomPizza = _random.Pick(comp.PizzaPrototypes);
                var pizza = Spawn(randomPizza, Transform(uid).Coordinates);

                if (!_container.Insert(pizza, container))
                {
                    QueueDel(pizza);
                }
            }
        }
    }
}