using Robust.Shared.GameStates;

namespace Content.Shared.Movement.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SwimmableMapComponent : Component
{
    /// <summary>
    /// Коэффициент сопротивления воды, применяемый ко всем существам,
    /// находящимся на данной карте. Чем выше значение — тем сильнее вода тормозит.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WaterResistance = 4f;
}
