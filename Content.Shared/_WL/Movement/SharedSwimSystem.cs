using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Robust.Shared.Map;

namespace Content.Shared.Movement.Systems;

public sealed partial class SharedSwimSystem : EntitySystem
{
    [Dependency] private EntityQuery<SwimmerComponent> _swimmerQuery = default!;
    [Dependency] private EntityQuery<SwimmableMapComponent> _swimmableMapQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Вода останавливает ВСЕХ существ, находящихся на карте с SwimmableMapComponent:
        // для всех них срабатывает IsWeightless = true. Дальнейшая проверка на возможность
        // двигаться в воде выполняется только для существ с SwimmerComponent.
        // Подписываемся на IsWeightlessEvent через GravityAffectedComponent, чтобы получить
        // доступ к EntityUid конкретной сущности (событие не содержит EntityUid).
        SubscribeLocalEvent<GravityAffectedComponent, IsWeightlessEvent>(OnIsWeightless);
        SubscribeLocalEvent<SwimmerComponent, CanWeightlessMoveEvent>(OnSwimmerCanWeightlessMove);
        SubscribeLocalEvent<SwimmerComponent, RefreshWeightlessModifiersEvent>(OnSwimmerRefreshWeightless);
    }

    private void OnIsWeightless(Entity<GravityAffectedComponent> entity, ref IsWeightlessEvent args)
    {
        // Если сущность уже считается weightless по каким-то другим причинам —
        // не переопределяем её состояние водой.
        if (args.IsWeightless)
            return;

        // Если у сущности нет Transform — пропускаем. Это может произойти, если
        // IsWeightlessEvent распространяется через инвентарь на entity, у которой
        // нет собственного TransformComponent.
        if (!_xformQuery.TryComp(entity.Owner, out var xform))
            return;

        // Если сущность не находится в воде — выходим.
        if (!IsInWater(xform))
            return;

        // Сущность в воде — делаем её weightless, что заставит SharedMoverController
        // и TileFrictionController применить сопротивление воды.
        // НЕ устанавливаем args.Handled = true, чтобы не блокировать другие
        // обработчики (например, антиграв-одежду).
        args.IsWeightless = true;
    }

    private void OnSwimmerCanWeightlessMove(Entity<SwimmerComponent> entity, ref CanWeightlessMoveEvent args)
    {
        // Только существа с SwimmerComponent могут двигаться в воде.
        if (_xformQuery.TryComp(entity.Owner, out var xform) && IsInWater(xform))
            args.CanMove = true;
    }

    private void OnSwimmerRefreshWeightless(Entity<SwimmerComponent> entity, ref RefreshWeightlessModifiersEvent args)
    {
        if (!_xformQuery.TryComp(entity.Owner, out var xform) || !IsInWater(xform))
            return;

        args.ModifyAcceleration(entity.Comp.SwimAccelerationModifier, entity.Comp.SwimSpeedModifier);
    }

    /// <summary>
    /// Быстрая проверка "в воде ли сущность" по TransformComponent.
    /// Позволяет избежать резолва TransformComponent у вызывающего.
    /// </summary>
    public bool IsInWater(TransformComponent xform)
    {
        if (xform.GridUid != null)
            return false;

        var mapUid = xform.MapUid;
        if (mapUid is not { Valid: true } || xform.MapID == MapId.Nullspace)
            return false;

        return _swimmableMapQuery.HasComp(mapUid);
    }

    /// <summary>
    /// Проверка "в воде ли сущность" по EntityUid.
    /// </summary>
    public bool IsInWater(EntityUid uid)
    {
        return _xformQuery.TryComp(uid, out var xform) && IsInWater(xform);
    }

    /// <summary>
    /// Возвращает коэффициент сопротивления воды, если сущность находится в воде.
    /// Иначе возвращает null. Позволяет избежать двойного резолва SwimmableMapComponent —
    /// сначала для проверки "в воде ли", затем для получения WaterResistance.
    /// </summary>
    public float? TryGetWaterResistance(TransformComponent xform)
    {
        if (xform.GridUid != null)
            return null;

        var mapUid = xform.MapUid;
        if (mapUid is not { Valid: true } || xform.MapID == MapId.Nullspace)
            return null;

        return _swimmableMapQuery.TryComp(mapUid, out var swimmableMap)
            ? swimmableMap.WaterResistance
            : null;
    }
}
