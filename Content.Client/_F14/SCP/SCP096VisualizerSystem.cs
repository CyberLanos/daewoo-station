using Content.Shared._F14.SCP;
using Robust.Client.GameObjects;

namespace Content.Client._F14.SCP;

// Оскільки ми наслідуємося від VisualizerSystem<SCP096Component>, 
// підписка на AppearanceChangeEvent вже зроблена автоматично базовим класом.
public sealed class SCP096VisualizerSystem : VisualizerSystem<SCP096Component>
{
    // Метод Initialize тепер порожній або може бути видалений, якщо ти нічого іншого там не робиш
    public override void Initialize()
    {
        base.Initialize();
    }

    // Тобі просто потрібно перевантажити (override) цей метод, 
    // який автоматично викликається базовою системою.
    protected override void OnAppearanceChange(EntityUid uid, SCP096Component component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // Отримуємо стан від сервера
        if (!AppearanceSystem.TryGetData<SCP096State>(uid, SCP096Visuals.State, out var state, args.Component))
            return;

        switch (state)
        {
            case SCP096State.Idle:
                args.Sprite.LayerSetState(0, "alive");
                args.Sprite.Rotation = Angle.Zero; // Повертаємо у вертикальне положення
                break;
            case SCP096State.Charging:
                args.Sprite.LayerSetState(0, "screaming");
                args.Sprite.Rotation = Angle.Zero;
                break;
            case SCP096State.Enraged:
                args.Sprite.LayerSetState(0, "running");
                args.Sprite.Rotation = Angle.Zero;
                break;
            case SCP096State.Dead:
                args.Sprite.LayerSetState(0, "dead");
                // Повертаємо спрайт на 90 градусів, щоб він "лежав"
                args.Sprite.Rotation = Angle.FromDegrees(90);
                break;
        }
    }
}