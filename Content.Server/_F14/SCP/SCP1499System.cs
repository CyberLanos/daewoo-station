using Content.Shared._F14.SCP;
using Content.Shared.Inventory.Events;

namespace Content.Server._F14.SCP;

public sealed class SCP1499System : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SCP1499Component, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<SCP1499Component, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(EntityUid uid, SCP1499Component comp, GotEquippedEvent args)
    {
        if (args.Slot != "mask" && args.Slot != "head")
            return;

        var player = args.Equipee;

        comp.SavedLocation = Transform(player).Coordinates;
        comp.CurrentUser = player;


        var dimensionQuery = EntityQueryEnumerator<SCP1499DimensionComponent, TransformComponent>();
        if (dimensionQuery.MoveNext(out var dimUid, out _, out var dimXform))
        {

            _transform.SetCoordinates(player, dimXform.Coordinates);
        }
        else
        {
            Logger.Warning("[SCP-1499] SCP1499DimensionSpawn Is not found!");
        }
    }

    private void OnUnequipped(EntityUid uid, SCP1499Component comp, GotUnequippedEvent args)
    {
        var player = args.Equipee;

        if (comp.CurrentUser != player)
            return;

        if (comp.SavedLocation != null)
        {
            _transform.SetCoordinates(player, comp.SavedLocation.Value);

            comp.SavedLocation = null;
            comp.CurrentUser = null;
        }
    }
}