using Content.Shared._F14.SCP;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Stacks;

namespace Content.Server._F14.SCP;

public sealed class SCP294System : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SCP294Component, SCP294InsertQuarterMessage>(OnInsertQuarter);
        SubscribeLocalEvent<SCP294Component, SCP294RequestLiquidMessage>(OnRequestLiquid);
    }

    private void UpdateUI(EntityUid uid, SCP294Component comp, string? message = null)
    {
        if (_uiSystem.HasUi(uid, SCP294UiKey.Key))
        {
            _uiSystem.SetUiState(uid, SCP294UiKey.Key, new SCP294BuiState(comp.QuartersRequired, comp.QuartersInserted, message));
        }
    }

    private void OnInsertQuarter(EntityUid uid, SCP294Component comp, SCP294InsertQuarterMessage args)
    {
        var player = args.Actor;

        if (_hands.TryGetActiveItem(player, out var heldItem) && heldItem != null)
        {
            var meta = MetaData(heldItem.Value);

            if (meta.EntityPrototype != null && meta.EntityPrototype.ID == "Quater")
            {
                QueueDel(heldItem.Value);

                comp.QuartersInserted++;
                UpdateUI(uid, comp);
                return;
            }
        }
    }
    private void OnRequestLiquid(EntityUid uid, SCP294Component comp, SCP294RequestLiquidMessage args)
    {
        if (args.Actor == EntityUid.Invalid) return;

        if (comp.QuartersInserted < comp.QuartersRequired)
        {
            UpdateUI(uid, comp, "Insert more quarters");
            return;
        }

        var liquidName = args.LiquidName.ToLower().Trim();
        ReagentPrototype? foundReagent = null;

        foreach (var reagent in _prototypeManager.EnumeratePrototypes<ReagentPrototype>())
        {
            if (reagent.ID.ToLower() == liquidName || Loc.GetString(reagent.LocalizedName).ToLower() == liquidName)
            {
                foundReagent = reagent;
                break;
            }
        }

        if (foundReagent == null)
        {
            UpdateUI(uid, comp, "OUT OF RANGE");
            return;
        }

        var containerProto = comp.ContainerPrototype;
        if (!_prototypeManager.HasIndex<EntityPrototype>(containerProto))
        {
            if (_prototypeManager.HasIndex<EntityPrototype>("DrinkGlass")) containerProto = "DrinkGlass";
            else if (_prototypeManager.HasIndex<EntityPrototype>("DrinkMug")) containerProto = "DrinkMug";
            else if (_prototypeManager.HasIndex<EntityPrototype>("FoodCupPaper")) containerProto = "FoodCupPaper";
            else
            {
                Logger.Error($"[SCP-294] WHO DELETED CUP PROTOTYPE?!");
                return;
            }
        }

        var coords = _transform.GetMapCoordinates(uid);
        var cup = Spawn(containerProto, coords);

        if (_solutionContainer.TryGetSolution(cup, "drink", out var solutionEntity))
        {
            _solutionContainer.TryAddReagent(solutionEntity.Value, foundReagent.ID, FixedPoint2.New(comp.DispenserAmount), out _);
        }


        comp.QuartersInserted -= comp.QuartersRequired;


        UpdateUI(uid, comp, "DISPENSED");
    }
}