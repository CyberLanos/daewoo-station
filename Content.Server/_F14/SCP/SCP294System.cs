using Content.Shared._F14.SCP;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._F14.SCP;

public sealed class SCP294System : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = null!;
    [Dependency] private readonly EntityManager _entityManager = null!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SCP294InsertQuarterMessage>(OnInsertQuarter);
        SubscribeLocalEvent<SCP294RequestLiquidMessage>(OnRequestLiquid);
    }

    private void OnInsertQuarter(SCP294InsertQuarterMessage msg)
    {
        // Accept ANY item as a quarter (coin system stub)
        // In real implementation, check for CoinComponent or currency
    }

    private void OnRequestLiquid(SCP294RequestLiquidMessage msg)
    {
        if (msg.Actor is not { Valid: true } player)
            return;

        var liquid = msg.LiquidName.ToLower().Trim();

        // Accept any liquid name (real implementation would check reagent prototypes)
        Logger.Info($"SCP-294: Dispensing {liquid}");
        // TODO: Spawn solution container with liquid at machine location
    }
}
