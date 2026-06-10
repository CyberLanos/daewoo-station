using Content.Shared.EntityEffects;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes; // ДОДАНО: Для читання з YAML
using Robust.Shared.Timing;
using System;

namespace Content.Shared._F14.SCP;

[DataDefinition] // ДОДАНО: Без цього рушій "не бачить" ефект у YAML!
public sealed partial class RPMessageEffect : EntityEffect
{
    [DataField("message")]
    public string Message = "You forgot something";

    public override void Effect(EntityEffectBaseArgs args)
    {
        var uid = args.TargetEntity; 
        var entityManager = args.EntityManager;
        var timing = IoCManager.Resolve<IGameTiming>();

        if (!entityManager.TryGetComponent<AmnesticMarkerComponent>(uid, out var marker))
            marker = entityManager.EnsureComponent<AmnesticMarkerComponent>(uid);

        if (timing.CurTime - marker.LastMessageTime > TimeSpan.FromMinutes(2))
        {
            var popup = entityManager.System<SharedPopupSystem>();
            
            popup.PopupEntity(Message, uid, uid, PopupType.LargeCaution);
            
            marker.LastMessageTime = timing.CurTime;
        }
    }

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return "Стирає пам'ять та викликає дезорієнтацію.";
    }
}

[RegisterComponent]
public sealed partial class AmnesticMarkerComponent : Component
{
    public TimeSpan LastMessageTime = TimeSpan.Zero;
}