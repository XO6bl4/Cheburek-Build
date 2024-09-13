using Content.Server.Radium.Medical.Surgery.Systems;
using Content.Shared.Radium.Nanites.Systems;
using Content.Shared.Radium.Nanites.Events;
using Robust.Shared.Timing;
using Content.Shared.Alert;
using Content.Shared.Radium.Nanites.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed.Commands.Math;
using Robust.Shared.Utility;
using Content.Shared.Damage;
using Content.Server.Body.Systems;
using Content.Shared.Prototypes;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Shared.Damage.Prototypes;

namespace Content.Server.Radium.Nanites.Programs;

public abstract class NanitesServerPrograms : SharedNanitesSystem
{
    [Dependency] private SharedNanitesSystem _nanitesSystem = default!;
    [Dependency] private IGameTiming _gameTicker = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SolutionContainerSystem _solutions = default!;
    [Dependency] private BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private DamageSpecifier _specifier = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public void RegenerateHealthByNanites(EntityUid uid, EntityUid user, NanitesComponent component, EventArgs? args)
    {
        var damagePerSecond = 1;
        var damageable = EnsureComp<DamageableComponent>(uid);

        _nanitesSystem.TryTakeNanites(uid, damagePerSecond, component);
        _damage.TryChangeDamage(uid, );
    }


}
